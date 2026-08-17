using System;
using System.Collections.Generic;
using Game.Runtime.GameMode.Poker.Abilities;
using Game.Runtime.GameMode.Poker.Player;
using Game.Runtime.GameMode.Poker.Stages;
using Game.Runtime.Player;
using Sirenix.OdinInspector;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Modules
{
	// The ability game, plugged into the table as one module: a pool dealt out every hand, cheats
	// that tag their user, and the report that calls the tag. Guilt lives server-side in plain
	// collections — replicating it would hand the answer to the very players supposed to be
	// guessing. Everything a client needs rides replicated state instead of fired-off RPCs: the
	// card sits owner-read on the player, and whether the windows are open is decided here and
	// replicated, so the UI never has to guess at server config.
	public class PokerAbilityModule : PokerModule
	{
		[Header("Deal")]
		[SerializeField] private PokerAbilityPool _pool;

		[Tooltip("How many cheats are guaranteed in the deal, rolled fresh each hand between these two. A fixed count is a tell — once the table learns the number, the arithmetic does the guessing for them.")]
		[MinMaxSlider(0, 12, true)]
		[SerializeField] private Vector2Int _guaranteedCheatCards = new(1, 2);

		[Tooltip("How many abilities each seat is dealt. The wheel is what the player picks between, so more than one is the point of having it.")]
		[MinValue(1)]
		[SerializeField] private int _abilitiesPerSeat = 2;

		[Header("Ability Use")]
		[Tooltip("Stages abilities may be used in, matched by stage id. Empty allows every stage.")]
		[SerializeField] private List<PokerStage> _abilityStages = new();

		[Tooltip("Seconds from a stage opening that abilities stay usable. Zero or less keeps the window open for the whole stage.")]
		[SerializeField] private float _abilityWindowSeconds;

		[Header("Report")]
		[SerializeField] private int _reportsPerRound = 1;

		[Tooltip("Staked when there is no report overlay to haggle over it. With one, the accused names the number themselves and this is never read.")]
		[SerializeField] private int _reportStake = 100;

		[Tooltip("Stages a report may be filed in, matched by stage id. Empty allows any moment of the hand.")]
		[SerializeField] private List<PokerStage> _reportStages = new();

		[Tooltip("Overlay shown while the verdict lands. Empty resolves on the spot with no pause.")]
		[SerializeField] private PokerStage _reportStage;

		[Header("Toggle")]
		[Tooltip("Where the switch starts. Flip the replicated value at runtime to turn the whole ability game on or off.")]
		[SerializeField] private bool _enabledByDefault = true;

		[HideInInspector] public NetworkVariable<bool> Enabled = new(true,
			readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);

		// The module's standing decisions, made on the server and replicated — a client's UI shows the
		// ability game exactly when these say it is on, whatever stages and windows are configured.
		[HideInInspector] public NetworkVariable<bool> AbilityWindowOpen = new(false,
			readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);

		[HideInInspector] public NetworkVariable<bool> ReportWindowOpen = new(false,
			readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);

		[HideInInspector] public NetworkVariable<PokerReportResult> LastReport = new(default,
			readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);

		// Published as the report is filed, so every table shows the accusation while the verdict is
		// still being weighed. Its sequence is the one the matching verdict will carry.
		[HideInInspector] public NetworkVariable<PokerReportAccusation> Accusation = new(default,
			readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);

		// What the accused has put up to be believed. Zero until they answer — the accuser is being asked to
		// match a number, so it has to be on the table before they can say anything about it.
		[HideInInspector] public NetworkVariable<int> ReportStake = new(0,
			readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);

		// Server-only truth. Which card each player holds and who cheated never replicate in the clear —
		// the report game is a guessing game, and this is the answer sheet.
		private readonly Dictionary<ulong, List<PokerAbility>> _held = new();
		private readonly HashSet<ulong> _cheaters = new();
		private readonly List<PokerAbility> _dealBuffer = new();

		private ulong _pendingAccuser;
		private ulong _pendingTarget;
		private bool _hasPendingReport;
		private double _stageOpenedTime;

		// Clients resolve their owner-read ability id against the same pool asset the server deals from.
		public PokerAbilityPool Pool => _pool;

		// Read by the report overlay as it opens: it runs the haggling, this holds the answer sheet.
		public bool HasPendingReport => _hasPendingReport;
		public ulong PendingAccuserClientId => _pendingAccuser;
		public ulong PendingTargetClientId => _pendingTarget;

		public override void OnNetworkSpawn()
		{
			base.OnNetworkSpawn();

			if (IsServer) Enabled.Value = _enabledByDefault;
		}

		// The report overlay never joins the loop, but every client renders it and reads its numbers off the
		// clone, so it is named here rather than left to the server's push to conjure up.
		public override void CollectReferencedStages(List<PokerStage> stages)
		{
			if (_reportStage) stages.Add(_reportStage);
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

		public override void OnGameEnded() => ClearRoundServer();

		// A NetworkBehaviour update, not a poll for a dependency: the window closes on a clock, and
		// somebody has to notice the moment it does.
		private void Update()
		{
			if (!IsServer || !IsSpawned) return;

			RefreshWindowsServer();
		}

		private void RefreshWindowsServer()
		{
			var running = Enabled.Value && GameMode && GameMode.IsGameRunning;
			var uninterrupted = GameMode && GameMode.CurrentOverlay == null;

			var abilityOpen = running && uninterrupted && IsStageAllowed(_abilityStages) && IsInsideAbilityWindow();
			if (AbilityWindowOpen.Value != abilityOpen) AbilityWindowOpen.Value = abilityOpen;

			var reportOpen = running && uninterrupted && !_hasPendingReport && IsStageAllowed(_reportStages);
			if (ReportWindowOpen.Value != reportOpen) ReportWindowOpen.Value = reportOpen;
		}

		private void DealServer()
		{
			if (!IsServer) return;

			ClearRoundServer();

			if (!Enabled.Value || !_pool) return;

			var seats = GameMode.Seats;

			// Rolled per hand, inclusive of both ends, so the number of cheats in circulation is itself
			// something the table cannot count on.
			var cheats = UnityEngine.Random.Range(_guaranteedCheatCards.x, _guaranteedCheatCards.y + 1);

			_pool.DrawDeal(seats.Count * _abilitiesPerSeat, cheats, _dealBuffer);

			for (var i = 0; i < seats.Count; i++)
			{
				var seat = seats[i];

				// An empty chair's cards stay on the table unheld — they exist so the number of cheats
				// in circulation never betrays who drew one.
				if (!seat || !seat.IsOccupied) continue;

				var player = GameMode.FindSeatedPlayer(seat.OccupantClientId);
				if (!player || !player.Data.IsInHand) continue;

				var hand = new List<PokerAbility>();
				var data = player.Data;

				for (var slot = 0; slot < _abilitiesPerSeat; slot++)
				{
					var index = i * _abilitiesPerSeat + slot;
					if (index >= _dealBuffer.Count) break;

					var ability = _dealBuffer[index];
					if (!ability) continue;

					hand.Add(ability);
					data.AbilityIds.Add(ability.AbilityId);
				}

				_held[player.ClientId] = hand;
				data.ReportsLeft.Value = _reportsPerRound;
			}
		}

		private void ClearRoundServer()
		{
			if (!IsServer || !GameMode) return;

			_held.Clear();
			_cheaters.Clear();
			_hasPendingReport = false;

			foreach (var player in GameMode.SeatedPlayers)
			{
				if (!player || !player.Data) continue;

				player.Data.AbilityIds.Clear();
				player.Data.ReportsLeft.Value = 0;
			}
		}

		// Named rather than implied: the player holds several and the wheel says which one they turned up,
		// so the server is told the choice instead of inferring it from a hand it would have to assume the
		// order of.
		[Rpc(SendTo.Server)]
		public void UseAbilityRPC(FixedString64Bytes abilityId, RpcParams rpcParams = default)
		{
			var clientId = rpcParams.Receive.SenderClientId;

			// The replicated window is the same decision the UI showed — the server just has the
			// final word on it.
			if (!AbilityWindowOpen.Value) return;

			var player = GameMode.FindSeatedPlayer(clientId);
			if (!player || !player.Data.IsInHand) return;
			if (!_held.TryGetValue(clientId, out var hand) || hand == null) return;

			var index = hand.FindIndex(candidate => candidate && candidate.AbilityId == abilityId.ToString());
			if (index < 0) return;

			var ability = hand[index];
			if (!ability.ActivateServer(GameMode, player)) return;

			// Only the owner's replica learns the card is spent. Nobody else hears a thing — an
			// ability is played in silence, and the report game is how it ever comes to light.
			hand.RemoveAt(index);
			RemoveFirst(player.Data.AbilityIds, abilityId);

			if (ability.Kind == PokerAbilityKind.Cheat) _cheaters.Add(clientId);
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

		[Rpc(SendTo.Server)]
		public void ReportRPC(ulong targetClientId, RpcParams rpcParams = default)
		{
			var accuser = rpcParams.Receive.SenderClientId;

			if (!ReportWindowOpen.Value) return;
			if (_hasPendingReport || accuser == targetClientId) return;

			// A folded player has no voice left this hand — they can be accused, not accuse.
			var accuserPlayer = GameMode.FindSeatedPlayer(accuser);
			if (!accuserPlayer || !accuserPlayer.Data.IsInHand) return;
			if (accuserPlayer.Data.ReportsLeft.Value <= 0) return;

			// The accused only has to have been dealt in: folding out does not launder a cheat.
			var targetPlayer = GameMode.FindSeatedPlayer(targetClientId);
			if (!targetPlayer || !WasDealtIn(targetPlayer)) return;

			accuserPlayer.Data.ReportsLeft.Value -= 1;

			_pendingAccuser = accuser;
			_pendingTarget = targetClientId;
			_hasPendingReport = true;

			Accusation.Value = new PokerReportAccusation
			{
				AccuserClientId = accuser,
				TargetClientId = targetClientId,
				Sequence = LastReport.Value.Sequence + 1
			};

			ReportStake.Value = 0;

			// The accusation is a scene: the finger jabs across the table, the accused startles. Played as
			// it is filed, so the table sees who started it before the overlay even lands.
			accuserPlayer.ActionAnimator?.ServerPlay(PlayerActionIds.Report);
			targetPlayer.ActionAnimator?.ServerPlay(PlayerActionIds.Reported);

			if (_reportStage) GameMode.PushOverlay(_reportStage);
			else ResolvePendingReportServer(_reportStake, true);
		}

		// The verdict, once the challenge has been answered. Called by the report overlay, which is what
		// decides the stake and whether the accuser was willing to pay it — or directly with the configured
		// stake when no overlay is set up to haggle over one.
		//
		// A challenge nobody called is dropped rather than judged: the accused keeps their secret, nothing
		// moves, and the accusation is spent all the same.
		public void ResolvePendingReportServer(int stake, bool called)
		{
			if (!IsServer || !_hasPendingReport) return;

			_hasPendingReport = false;

			var accuser = GameMode.FindSeatedPlayer(_pendingAccuser);
			var target = GameMode.FindSeatedPlayer(_pendingTarget);
			if (accuser == null || target == null) return;

			var wasCheater = called && _cheaters.Contains(_pendingTarget);
			var amount = 0;

			if (called)
			{
				var loser = wasCheater ? target : accuser;
				var winner = wasCheater ? accuser : target;

				var wallet = loser.Wallet;
				amount = Math.Min(Math.Max(0, stake), wallet ? wallet.Money.Value : 0);
				if (amount > 0 && wallet.ServerTryWithdraw(amount)) winner.Data.ServerWinChips(amount);

				// A cheat caught in the act is out of the hand as well as out of pocket. Pointing at the wrong
				// player only costs money: the accuser plays on, which is what keeps a hunch worth acting on
				// at all rather than a way to talk yourself out of the hand.
				if (wasCheater) FoldServer(target);

				// The verdict on their faces: whoever the money just left hangs their head, whoever it came
				// to laughs at them.
				accuser.ActionAnimator?.ServerPlay(wasCheater ? PlayerActionIds.Laugh : PlayerActionIds.Disappointed);
				target.ActionAnimator?.ServerPlay(wasCheater ? PlayerActionIds.Disappointed : PlayerActionIds.Laugh);
			}

			LastReport.Value = new PokerReportResult
			{
				AccuserClientId = _pendingAccuser,
				TargetClientId = _pendingTarget,
				Called = called,
				WasCheater = wasCheater,
				Amount = amount,
				Sequence = LastReport.Value.Sequence + 1
			};
		}

		private void FoldServer(PokerPlayer player)
		{
			// Already folded — the report only costs them the stake.
			if (!player.Data.IsInHand) return;

			player.Data.Status.Value = PokerPlayerStatus.Folded;
			player.Data.HasActed.Value = true;

			// Folded from outside their own action path: a street waiting on them would wait forever,
			// so it hears the same thing a departure would tell it. Under the report overlay the turn
			// slot is already parked, and the paused street re-reads the table when it resumes.
			if (Data.CurrentTurnClientId.Value != player.ClientId) return;

			var seatIndex = player.Data.SeatIndex.Value;
			GameMode.ClearTurn();
			GameMode.ActiveStage?.HandlePlayerLeft(player.ClientId, seatIndex);
		}

		private static bool WasDealtIn(PokerPlayer player)
		{
			return player.Data.IsInHand || player.Data.Status.Value == PokerPlayerStatus.Folded;
		}

		// Empty list keeps the gate open — restriction is opt-in, per the configured stage ids.
		private bool IsStageAllowed(List<PokerStage> stages)
		{
			if (stages.Count == 0) return true;

			var active = GameMode.ActiveStage;
			if (!active) return false;

			foreach (var stage in stages)
			{
				if (stage && stage.StageId == active.StageId) return true;
			}

			return false;
		}

		private bool IsInsideAbilityWindow()
		{
			if (_abilityWindowSeconds <= 0f || !NetworkManager) return true;

			return NetworkManager.ServerTime.Time - _stageOpenedTime <= _abilityWindowSeconds;
		}
	}
}
