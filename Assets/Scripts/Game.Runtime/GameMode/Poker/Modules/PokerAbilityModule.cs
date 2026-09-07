using System.Collections.Generic;
using Game.Runtime.GameMode.Config;
using Game.Runtime.GameMode.Poker.Abilities;
using Game.Runtime.GameMode.Poker.Player;
using Game.Runtime.GameMode.Poker.Stages;
using Sirenix.OdinInspector;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Modules
{
	// What a player is carrying and when they may play it. Nothing here knows about accusations: an
	// ability is a thing you do, and whether anybody may call it a cheat belongs to PokerReportModule,
	// which Hold'em runs alongside this and the mushroom round does not.
	//
	// The hand rides owner-read replicated state rather than fired-off RPCs, so a client that spawns
	// late still arrives knowing what it holds; whether the window is open is decided here and
	// replicated, so the UI never has to guess at server config.
	public class PokerAbilityModule : PokerModule
	{
		[Header("Pool")]
		[Required]
		[SerializeField] private PokerAbilityPool _pool;

		[Header("Dealing")]
		[Tooltip("Dealt to every player when a hand begins. Zero deals nobody anything, which is what a table that only hands items to losers wants.")]
		[MinValue(0)]
		[SerializeField] private int _abilitiesPerSeat = 2;

		[Tooltip("Handed to each player who loses a hand, on top of whatever they were already carrying. This is the round's reward for coming last.")]
		[MinValue(0)]
		[SerializeField] private int _abilitiesPerLoss;

		[Tooltip("Most a player may be holding at once. Zero or less is no ceiling at all.")]
		[MinValue(0)]
		[SerializeField] private int _handLimit;

		[Tooltip("On, a hand is emptied when the next one is dealt: what was given is for that hand only. Off, unspent abilities carry forward, which is what an item a player is stockpiling has to do.")]
		[SerializeField] private bool _clearHandEachRound = true;

		[Tooltip("How many cheats are guaranteed in the deal, rolled fresh each hand between these two. A fixed count is a tell. Only meaningful where a report module is running.")]
		[MinMaxSlider(0, 12, true)]
		[SerializeField] private Vector2Int _guaranteedCheatCards = new(1, 2);

		[Header("Use")]
		[Tooltip("Stages abilities may be used in, matched by stage id. Empty allows every stage.")]
		[SerializeField] private List<PokerStage> _abilityStages = new();

		[Tooltip("Seconds from a stage opening that abilities stay usable. Zero or less keeps the window open for the whole stage.")]
		[SerializeField] private float _abilityWindowSeconds;

		[Header("Toggle")]
		[Tooltip("Where the switch starts. Flip the replicated value at runtime to turn the whole ability game on or off.")]
		[SerializeField] private bool _enabledByDefault = true;

		[HideInInspector] public NetworkVariable<bool> Enabled = new(true,
			readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);

		// The module's standing decision, made on the server and replicated: a client's wheel offers a
		// card exactly when this says it may be played, whatever stages and windows are configured.
		[HideInInspector] public NetworkVariable<bool> AbilityWindowOpen = new(false,
			readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);

		// What each player is holding, server-side. The owner's own copy rides PokerPlayerData.AbilityIds;
		// this is the server's, and it carries the assets rather than their ids.
		private readonly Dictionary<ulong, List<PokerAbility>> _held = new();

		// Who has played a cheat this hand. Server-side and plain: replicating it would hand the answer to
		// the very players a report is supposed to make guess. Read by PokerReportModule and nobody else.
		private readonly HashSet<ulong> _cheaters = new();

		private readonly List<PokerAbility> _dealBuffer = new();

		private double _stageOpenedTime;

		// Clients resolve their owner-read ability id against the same pool asset the server deals from.
		public PokerAbilityPool Pool => _pool;

		public bool HasPlayedCheat(ulong clientId) => _cheaters.Contains(clientId);

		public override void OnNetworkSpawn()
		{
			base.OnNetworkSpawn();

			if (IsServer) Enabled.Value = _enabledByDefault;
		}

		protected override void OnCollectConfigEntries(List<MatchConfigEntry> entries)
		{
			entries.Add(new MatchConfigInt(ModuleId, "Abilities", "AbilitiesPerSeat", "Abilities Per Seat", 0, 5, 1,
				() => _abilitiesPerSeat, value => _abilitiesPerSeat = value));

			entries.Add(new MatchConfigInt(ModuleId, "Abilities", "AbilitiesPerLoss", "Abilities Per Loss", 0, 5, 1,
				() => _abilitiesPerLoss, value => _abilitiesPerLoss = value));

			// The range is two entries with a cross-clamp, so min and max stay ordered whichever of the
			// two values a replicated edit lands first.
			entries.Add(new MatchConfigInt(ModuleId, "Abilities", "CheatCardsMin", "Cheat Cards Min", 0, 12, 1,
				() => _guaranteedCheatCards.x,
				value => _guaranteedCheatCards = new Vector2Int(value, Mathf.Max(value, _guaranteedCheatCards.y))));
			entries.Add(new MatchConfigInt(ModuleId, "Abilities", "CheatCardsMax", "Cheat Cards Max", 0, 12, 1,
				() => _guaranteedCheatCards.y,
				value => _guaranteedCheatCards = new Vector2Int(Mathf.Min(_guaranteedCheatCards.x, value), value)));
		}

		public void SetEnabledServer(bool enabled)
		{
			if (!IsServer) return;

			Enabled.Value = enabled;
		}

		public override void OnStageStarted(PokerStage stage)
		{
			_stageOpenedTime = NetworkManager ? NetworkManager.ServerTime.Time : 0d;

			if (stage is PokerDealStage) DealServer();
		}

		public override void OnGameEnded() => ClearMatchServer();

		// A NetworkBehaviour update, not a poll for a dependency: the window closes on a clock, and
		// somebody has to notice the moment it does.
		private void Update()
		{
			if (!IsServer || !IsSpawned) return;

			var running = Enabled.Value && GameMode && GameMode.IsGameRunning;
			var uninterrupted = GameMode && GameMode.CurrentOverlay == null;
			var open = running && uninterrupted && IsStageAllowed(_abilityStages) && IsInsideAbilityWindow();

			if (AbilityWindowOpen.Value != open) AbilityWindowOpen.Value = open;
		}

		private void DealServer()
		{
			if (!IsServer) return;

			// Only what belongs to one hand is cleared. A table carrying hands forward keeps them, and the
			// cheat record is per-hand either way: an accusation is about this hand's play.
			_cheaters.Clear();
			if (_clearHandEachRound) ClearHandsServer();

			if (!Enabled.Value || !_pool || _abilitiesPerSeat <= 0) return;

			var seats = GameMode.Seats;

			// Rolled per hand, inclusive of both ends, so the number of cheats in circulation is itself
			// something the table cannot count on.
			var cheats = Random.Range(_guaranteedCheatCards.x, _guaranteedCheatCards.y + 1);

			_pool.DrawDeal(seats.Count * _abilitiesPerSeat, cheats, _dealBuffer);

			for (var i = 0; i < seats.Count; i++)
			{
				var seat = seats[i];

				// An empty chair's cards stay on the table unheld: they exist so the number of cheats in
				// circulation never betrays who drew one.
				if (!seat || !seat.IsOccupied) continue;

				var player = GameMode.FindSeatedPlayer(seat.OccupantClientId);
				if (!player || !player.Data.IsInHand) continue;

				for (var slot = 0; slot < _abilitiesPerSeat; slot++)
				{
					var index = i * _abilitiesPerSeat + slot;
					if (index >= _dealBuffer.Count) break;

					GiveServer(player, _dealBuffer[index]);
				}
			}
		}

		// Handed to whoever lost, by the settlement rather than by a stage hook: "who lost" is a question
		// only the settlement has answered.
		public void GiveLossRewardServer(PokerPlayer player)
		{
			if (!IsServer || !Enabled.Value || !_pool || _abilitiesPerLoss <= 0) return;
			if (!player || !player.Data) return;

			_dealBuffer.Clear();
			_pool.DrawDeal(_abilitiesPerLoss, 0, _dealBuffer);

			foreach (var ability in _dealBuffer) GiveServer(player, ability);
		}

		// One place a card enters a hand, so the ceiling cannot be walked past by whichever caller forgets
		// it. A full hand silently keeps what it has: refusing loudly would announce to the table that
		// somebody is holding a full hand, which is theirs to know.
		private void GiveServer(PokerPlayer player, PokerAbility ability)
		{
			if (!ability || !player || !player.Data) return;

			if (!_held.TryGetValue(player.ClientId, out var hand) || hand == null)
			{
				hand = new List<PokerAbility>();
				_held[player.ClientId] = hand;
			}

			if (_handLimit > 0 && hand.Count >= _handLimit) return;

			hand.Add(ability);
			player.Data.AbilityIds.Add(ability.AbilityId);
		}

		// Named rather than implied: the player holds several and the wheel says which one they turned up,
		// so the server is told the choice instead of inferring it from a hand it would have to assume the
		// order of.
		[Rpc(SendTo.Server)]
		public void UseAbilityRPC(FixedString64Bytes abilityId, RpcParams rpcParams = default)
		{
			var clientId = rpcParams.Receive.SenderClientId;

			// The replicated window is the same decision the UI showed; the server just has the final word.
			if (!AbilityWindowOpen.Value) return;

			var player = GameMode.FindSeatedPlayer(clientId);
			if (!player || !player.Data.IsInHand) return;
			if (!_held.TryGetValue(clientId, out var hand) || hand == null) return;

			var index = hand.FindIndex(candidate => candidate && candidate.AbilityId == abilityId.ToString());
			if (index < 0) return;

			// A card that puts its holder on show for a few seconds cannot be covered by a second one
			// played behind it: the act is the only thing the table gets to read, so one at a time.
			if (IsBusy(player.Data))
			{
				Debug.LogWarning($"[{ModuleId}] Refused ability '{abilityId}' from client {clientId}: still busy with the last one.");
				return;
			}

			var ability = hand[index];
			if (!ability.ActivateServer(GameMode, player)) return;

			// Written after the activation, so a card that fizzles costs nothing and locks nothing.
			if (ability.BusySeconds > 0f)
			{
				player.Data.AbilityBusyUntil.Value = NetworkManager.ServerTime.Time + ability.BusySeconds;
			}

			// Only the owner's replica learns the card is spent. Nobody else hears a thing.
			hand.RemoveAt(index);
			RemoveFirst(player.Data.AbilityIds, abilityId);

			if (ability.Kind == PokerAbilityKind.Cheat) _cheaters.Add(clientId);
		}

		// Asked of the same replicated value the owner's wheel reads, so the button that greys out and the
		// server that refuses can never disagree about who is mid-trick.
		public bool IsBusy(PokerPlayerData data) =>
			data && NetworkManager && NetworkManager.ServerTime.Time < data.AbilityBusyUntil.Value;

		private void ClearHandsServer()
		{
			if (!GameMode) return;

			_held.Clear();

			foreach (var player in GameMode.SeatedPlayers)
			{
				if (!player || !player.Data) continue;

				player.Data.AbilityIds.Clear();
				player.Data.AbilityBusyUntil.Value = 0d;
			}
		}

		// The match ending empties everything whatever the per-hand setting says: a stockpile belongs to
		// the match it was built up in.
		private void ClearMatchServer()
		{
			if (!IsServer || !GameMode) return;

			_cheaters.Clear();
			ClearHandsServer();

			foreach (var player in GameMode.SeatedPlayers)
			{
				if (!player) continue;

				// A round torn down mid-swallow leaves somebody drunk over a table that is not there any
				// more, and a look at a hand that has already been put away.
				player.GetComponentInChildren<PokerDrinkController>()?.ServerClear();
			}
		}

		private bool IsInsideAbilityWindow()
		{
			if (_abilityWindowSeconds <= 0f || !NetworkManager) return true;

			return NetworkManager.ServerTime.Time - _stageOpenedTime <= _abilityWindowSeconds;
		}

		// One entry, not every match: a player dealt the same trick twice spends one copy and keeps the
		// other, the same way two identical cards in a hand are still two cards.
		private static void RemoveFirst(NetworkList<FixedString64Bytes> list, FixedString64Bytes value)
		{
			for (var i = 0; i < list.Count; i++)
			{
				if (!list[i].Equals(value)) continue;

				list.RemoveAt(i);
				return;
			}
		}
	}
}
