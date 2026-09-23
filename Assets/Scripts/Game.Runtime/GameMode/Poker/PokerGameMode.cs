using System;
using System.Collections.Generic;
using Game.Runtime.GameMode.Config;
using Game.Runtime.Controller;
using Game.Runtime.GameMode.Poker.Hands;
using Game.Runtime.GameMode.Poker.Modules;
using Game.Runtime.GameMode.Poker.Player;
using Game.Runtime.GameMode.Poker.Stages;
using Game.Runtime.Player;
using Game.Runtime.UI;
using Sirenix.OdinInspector;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker
{
	public class PokerGameMode : NetworkBehaviour, IGameMode, IMatchConfigProvider
	{
		[Header("Rules")]
		[SerializeField] private PokerRuleSettings _rules;

		[Header("Match")]
		[Tooltip("Blood every player starts a match with. Capped at 8 — PokerBloodFingerVisual draws MaxHealth minus health as severed fingers, and the model has eight.")]
		[Range(1, 8)]
		[SerializeField] private int _startingHealth = 8;

		[Tooltip("The kinds of cap this table is played with: what the bet bar offers, what a timeout bets and what the settlement hands round.")]
		[SerializeField] private BetItems.PokerBetItemDatabase _betItemDatabase;

		[Header("Stages")]
		[Tooltip("The round loop as a preset. Swap this asset to change the game — modules still add to it, and a stage can be queued ahead of the loop at runtime by InsertStage.")]
		[SerializeField] private PokerStageSequence _sequence;

		[Header("Modules")]
		[Tooltip("Plugged in features — each one may add stages, commands and restrictions of its own.")]
		[SerializeField] private List<PokerModule> _modules = new();

		[Header("UI")]
		[Tooltip("Spawned locally on every peer while the mode is alive. Presentation only, so it is a plain prefab and never travels over the network.")]
		[SerializeField] private GameObject _hudPrefab;

		[Header("References")]
		[SerializeField] private PokerGameData _data;
		[SerializeField] private MatchConfigData _configData;

		[Tooltip("Where everything the table is told goes out: accepted actions, items played, private news.")]
		[Required]
		[SerializeField] private PokerNoticeChannel _notices;
		[SerializeField] private List<PokerSeat> _seats = new();

		public static PokerGameMode Instance { get; private set; }

		// Raised when the table arrives or leaves, so views bind to it instead of watching for it.
		public static event Action<PokerGameMode> OnInstanceChanged;

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void ResetStatics()
		{
			Instance = null;
			OnInstanceChanged = null;
		}

		public PokerGameData Data => _data;
		public MatchConfigData ConfigData => _configData;
		public PokerNoticeChannel Notices => _notices;
		public BetItems.PokerBetItemDatabase BetItemDatabase => _betItemDatabase;
		public PokerRuleSettings Rules => _rules;
		public PokerStageSequence Sequence => _sequence;
		public PokerDeck Deck { get; } = new();
		public PokerHandEvaluator HandEvaluator { get; } = new();

		public IReadOnlyList<PokerSeat> Seats => _seats;
		public IReadOnlyList<PokerModule> Modules => _modules;
		public IReadOnlyList<PokerStage> Stages => _stageMachine != null ? _stageMachine.Stages : System.Array.Empty<PokerStage>();

		// Seated players in seat order — the order the turn passes around the table.
		public IReadOnlyList<PokerPlayer> SeatedPlayers => _seatedPlayers;

		// Sitting down is free, so a seat filled is not the same as a player who can play. Only the ones
		// still conscious make a hand worth dealing.
		public int DealablePlayerCount
		{
			get
			{
				var count = 0;

				foreach (var player in _seatedPlayers)
				{
					if (CanBeDealtIn(player.Data)) count++;
				}

				return count;
			}
		}

		public PokerStage CurrentStage => _stageMachine?.CurrentStage;

		public bool IsGameRunning => _data && _data.Phase.Value != PokerPhase.Waiting && _data.Phase.Value != PokerPhase.Finished;

		// One press of start begins a match, and a match is however many hands the table can still deal.
		// Asked at the end of each one, because a hand is exactly what takes players out of the running:
		// the same count the host's button is gated on, so a table that could be started can be continued.
		// Those collected into this match, not everyone in a chair: a table kept alive by spectators who
		// arrived after it began would deal hands nobody in them is playing.
		public bool CanDealAnotherHand => _rules && MatchPlayerCount >= _rules.MinimumPlayersToStart;

		// Whether the host may press start. The host holds the button rather than a hand, so their own seat
		// counts as company even when they have gone under — a table whose host is unconscious would otherwise
		// have no way back at all, since nothing revives a player between matches unless the idle stage is
		// told to. Pressing it does not bring them round: the deal marks them out of the running like anybody
		// else and they watch the match they started. This is a gate about who may press, never about who is
		// dealt in, which is why the deal goes on asking CanBeDealtIn for itself.
		public bool CanStartMatch
		{
			get
			{
				if (!_rules) return false;

				var count = DealablePlayerCount;
				var host = FindSeatedPlayer(NetworkManager.ServerClientId);

				if (host && host.Data && !CanBeDealtIn(host.Data)) count++;

				return count >= _rules.MinimumPlayersToStart;
			}
		}

		public event Action OnSeatedPlayersChanged;
		public event Action<PokerStage> OnStageChanged;

		// A module changed what somebody may do or what a bet costs. Raised on every peer.
		public event Action OnActionRulesChanged;

		private readonly List<PokerPlayer> _seatedPlayers = new();

		private PokerStageMachine _stageMachine;
		private GameObject _hudInstance;

		private void Awake()
		{
			if (Instance && Instance != this)
			{
				// Loud, and switched off rather than only destroyed. Destroy is deferred to the end of the
				// frame, so a duplicate left enabled goes on ticking with none of the state Awake would
				// have built for it — which reads as a NullReferenceException every frame out of Update
				// rather than as one line saying there are two tables in the scene.
				Debug.LogWarning($"[PokerGameMode] A second table is already running; '{name}' is standing down.", this);

				enabled = false;
				Destroy(gameObject);
				return;
			}

			Instance = this;

			_stageMachine = new PokerStageMachine(this, NotifyStageStarting, NotifyStageStarted, NotifyStageEnded);
		}

		public override void OnDestroy()
		{
			if (Instance == this) Instance = null;

			base.OnDestroy();
		}

		public override void OnNetworkSpawn()
		{
			if (_stageMachine == null) return;

			if (!_data) _data = GetComponentInChildren<PokerGameData>();
			if (_seats.Count == 0) CollectRegisteredSeats();

			_stageMachine.Build(_sequence, _modules);

			foreach (var module in _modules)
			{
				if (module) module.Initialize(this);
			}

			_stageMachine.InitializeStages();

			RegisterMatchConfigs();

			ServerLayTable();

			PokerPlayer.OnRegistryChanged += HandlePlayerRegistryChanged;
			HandlePlayerRegistryChanged();

			OnInstanceChanged?.Invoke(this);

			SpawnHud();

			if (!IsServer) return;

			NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnected;
			GoToStage(0);
		}

		public override void OnNetworkDespawn()
		{
			PokerPlayer.OnRegistryChanged -= HandlePlayerRegistryChanged;

			OnInstanceChanged?.Invoke(null);

			DespawnHud();

			if (IsServer && NetworkManager.Singleton)
			{
				NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnected;
			}

			_stageMachine.Shutdown();

			foreach (var module in _modules)
			{
				if (module) module.DeInitialize();
			}
		}

		private void Update()
		{
			// The machine is built in Awake, and the one path that skips it is the duplicate standing
			// down above. NGO spawns a scene object whether or not its component is enabled, so this
			// has to be asked here too rather than trusted to the disable.
			if (!IsServer || !IsSpawned || _stageMachine == null) return;

			_stageMachine.Tick(Time.deltaTime);
		}

		private void SpawnHud()
		{
			if (!_hudPrefab || _hudInstance) return;

			// The HUD is a panel, not a canvas of its own, so without the shared canvas there is
			// nowhere for it to draw — worth saying out loud rather than spawning something invisible.
			if (!UIManager.Instance)
			{
				Debug.LogWarning("[PokerGameMode] No UIManager found — the poker HUD needs the bootstrap canvas.");
				return;
			}

			_hudInstance = UIManager.Instance.Show(_hudPrefab, UILayer.Hud);
		}

		private void DespawnHud()
		{
			if (!_hudInstance) return;

			if (UIManager.Instance) UIManager.Instance.Hide(_hudInstance);
			else Destroy(_hudInstance);

			_hudInstance = null;
		}

		// Modules are plugged in by the inspector, so whatever needs one goes looking for it by type rather
		// than holding a reference to something that may not be at this table at all.
		public T FindModule<T>() where T : PokerModule
		{
			foreach (var module in _modules)
			{
				if (module is T match) return match;
			}

			return null;
		}

		public PokerStage FindStage(string stageId) => _stageMachine.Find(stageId);

		// Whether another street follows this one before the hand is scored. Read off the sequence every peer
		// builds, so the bar and the server agree without a word on the wire.
		public bool HasStreetAfter(PokerStage stage)
		{
			var stages = Stages;
			var index = -1;

			for (var i = 0; i < stages.Count; i++)
			{
				if (stages[i] != stage) continue;

				index = i;
				break;
			}

			if (index < 0) return false;

			for (var i = index + 1; i < stages.Count; i++)
			{
				if (stages[i] is PokerStreetStage) return true;
				if (stages[i] is PokerShowdownStage) return false;
			}

			return false;
		}

		public bool IsActionAllowed(PokerPlayerData player, PokerActionType action)
		{
			foreach (var module in _modules)
			{
				if (module && !module.IsActionAllowed(player, action)) return false;
			}

			return true;
		}

		public int ModifyStakeSize(PokerStreetStage street, int stakeSize)
		{
			foreach (var module in _modules)
			{
				if (module) stakeSize = module.ModifyStakeSize(street, stakeSize);
			}

			return Mathf.Max(1, stakeSize);
		}

		public void NotifyActionRulesChanged() => OnActionRulesChanged?.Invoke();

		public void NotifyHandSettled(IReadOnlyList<PokerPlayer> winners)
		{
			if (!IsServer) return;

			foreach (var module in _modules)
			{
				if (module) module.OnHandSettled(winners);
			}
		}

		// Only picks up seats that spawned before this table did; the ones that come later register
		// themselves on the way in.
		private void CollectRegisteredSeats()
		{
			_seats.Clear();
			_seats.AddRange(PokerSeat.All);
			_seats.Sort((left, right) => left.SeatIndex.CompareTo(right.SeatIndex));
		}

		public void RegisterSeat(PokerSeat seat)
		{
			if (!seat || _seats.Contains(seat)) return;

			_seats.Add(seat);
			_seats.Sort((left, right) => left.SeatIndex.CompareTo(right.SeatIndex));
		}

		public void UnregisterSeat(PokerSeat seat) => _seats.Remove(seat);

		// How many chairs to lay, taken from the lobby the table was opened for. Written once by the
		// server and read by everyone, so the chairs stand in the same places on every screen — a client
		// working it out from its own copy of the lobby would have nothing to warn it when they differed.
		private void ServerLayTable()
		{
			if (!IsServer || !_data) return;

			// The table says how many chairs it is laid with, not the lobby: how many people a room admits
			// and how many can sit at this table are different questions, and only the second one is a rule.
			var wanted = _rules ? _rules.SeatCount : _seats.Count;

			_data.ActiveSeatCount.Value = Mathf.Clamp(wanted, 0, _seats.Count);
		}

		// Whether this player can be dealt into the next hand. One predicate rather than the same test
		// written at every call site, so a second condition is one edit here.
		public bool CanBeDealtIn(PokerPlayerData data) => data && data.IsAlive;

		// A place in the match, as opposed to a chair in the room. Everything the round *does* to a player
		// asks this — dealing to them, letting them bet, feeding them a cap — so somebody who sat down
		// halfway through watches the rest of it out rather than being collected into a game that was
		// already scored around them.
		//
		// Deliberately not folded into CanBeDealtIn, which is asked *before* a match to decide whether one
		// can start at all: at that moment nobody is stamped in yet, and a gate reading this would make the
		// start button permanently dead.
		public bool IsPlayingThisMatch(PokerPlayerData data) => data && data.InMatch.Value && CanBeDealtIn(data);

		// Those still playing the match that is running. What decides whether there is another hand in it,
		// where DealablePlayerCount decides whether a new match can begin.
		public int MatchPlayerCount
		{
			get
			{
				var count = 0;

				foreach (var player in _seatedPlayers)
				{
					if (player && IsPlayingThisMatch(player.Data)) count++;
				}

				return count;
			}
		}

		// A body arriving is a body to seat: chairs are handed out rather than chosen, so this is where
		// somebody joining a table gets theirs. Seating raises the occupant change, which comes back
		// through HandleSeatOccupied and refreshes the list — so the refresh below is for the arrival
		// that found no free chair, and the pass terminates because a seated player is skipped.
		private void HandlePlayerRegistryChanged()
		{
			ServerSeatArrivals();
			RefreshSeatedPlayers();
		}

		// Lowest free chair, the same way PlayerManager claims the lowest free colour: a player who
		// leaves frees the chair they were in rather than renumbering everyone behind them.
		private void ServerSeatArrivals()
		{
			// The machine is built in Awake, and the one path that skips it is the duplicate standing
			// down above. NGO spawns a scene object whether or not its component is enabled, so this
			// has to be asked here too rather than trusted to the disable.
			if (!IsServer || !IsSpawned || _stageMachine == null) return;

			foreach (var player in PokerPlayer.All)
			{
				if (!player || !player.Data || player.Data.IsSeated) continue;

				var seatController = player.GetComponent<PlayerSeatController>();
				if (!seatController || seatController.IsSeated) continue;

				// Only the chairs the table is laid with: the rest are switched off and belong to a bigger
				// lobby than this one. Read off each chair's own SeatIndex rather than its place in this
				// list, because that is the key the ring lays them out by — matching on list position
				// instead put players in chairs that had been switched off, and it looked like a table
				// with two fewer seats than the scene contains.
				var laid = _data ? _data.ActiveSeatCount.Value : _seats.Count;

				for (var slot = 0; slot < laid; slot++)
				{
					var wanted = SpreadSeatIndex(slot, laid);
					var seat = FindSeat(wanted);
					if (!seat || seat.IsOccupied) continue;

					seat.SeatServer(seatController);
					break;
				}
			}
		}

		// Chairs are filled across the table rather than around it: with four laid, the order is 1, 3, 2, 4
		// so two players sit opposite each other instead of elbow to elbow with half the table empty.
		// Interleaving the two halves is the whole rule — every arrival lands as far from the last as the
		// remaining chairs allow, and it reads the same at any table size.
		private static int SpreadSeatIndex(int slot, int count)
		{
			if (count <= 0) return 0;

			// The first half is the *larger* half when the count is odd, or the two interleaved runs collide:
			// at three chairs, count / 2 sends slots 1 and 2 to the same seat and leaves one never used.
			return slot % 2 == 0 ? slot / 2 : (count + 1) / 2 + slot / 2;
		}

		private PokerSeat FindSeat(int seatIndex)
		{
			foreach (var seat in _seats)
			{
				if (seat && seat.SeatIndex == seatIndex) return seat;
			}

			return null;
		}

		public void RefreshSeatedPlayers()
		{
			_seatedPlayers.Clear();

			foreach (var player in PokerPlayer.All)
			{
				if (player && player.Data && player.Data.IsSeated) _seatedPlayers.Add(player);
			}

			_seatedPlayers.Sort((left, right) => left.Data.SeatIndex.Value.CompareTo(right.Data.SeatIndex.Value));
			OnSeatedPlayersChanged?.Invoke();

			// A body that arrived after the config seed still gets the configured stats — the prefab's own
			// self-reset at spawn only knows the authored default.
			ServerApplyStartingValues(resetPlayers: false);
		}

		public PokerPlayer FindSeatedPlayerAtSeat(int seatIndex)
		{
			if (seatIndex < 0) return null;

			foreach (var player in _seatedPlayers)
			{
				if (player && player.Data && player.Data.SeatIndex.Value == seatIndex) return player;
			}

			return null;
		}

		public PokerPlayer FindSeatedPlayer(ulong clientId)
		{
			foreach (var player in _seatedPlayers)
			{
				if (player.ClientId == clientId) return player;
			}

			return null;
		}

		// The room screen's view of this mode, taken off the prefab asset before any scene is loaded.
		// The entries built here are read for metadata and defaults only — the sequence walk hands out
		// the authored stage assets, and applying a value to those would write the asset itself.
		public void CollectAuthoredConfigEntries(List<MatchConfigEntry> entries)
		{
			CollectModeConfigEntries(entries);

			var stages = new List<PokerStage>();
			if (_sequence) _sequence.CollectStages(stages);

			foreach (var stage in stages)
			{
				if (stage) stage.CollectConfigEntries(entries);
			}

			foreach (var module in _modules)
			{
				if (module) module.CollectConfigEntries(entries);
			}
		}

		// The live registration, over the per-peer stage clones instead of the assets — the numbers the
		// pad and the server read sit on those clones, so that is where a replicated value has to land.
		private void RegisterMatchConfigs()
		{
			if (!_configData) return;

			var entries = new List<MatchConfigEntry>();
			CollectModeConfigEntries(entries);

			foreach (var stage in _stageMachine.Stages)
			{
				if (stage) stage.CollectConfigEntries(entries);
			}

			foreach (var module in _modules)
			{
				if (module) module.CollectConfigEntries(entries);
			}

			_configData.RegisterEntries(entries, () => _data && _data.Phase.Value == PokerPhase.Waiting);
		}

		private void CollectModeConfigEntries(List<MatchConfigEntry> entries)
		{
			entries.Add(new MatchConfigInt("Match", "Match", "StartingHealth", "Starting Health", 1, 8, 1,
				() => _startingHealth,
				value =>
				{
					_startingHealth = value;
					ServerApplyStartingValues(resetPlayers: true);
				}));
		}

		// An edit while the table is waiting re-resets every body on the spot, so the readouts answer to the
		// new numbers without anyone re-seating. Mid-match only a body that never got the configured stats
		// at all is touched — a fresh one is not in a hand.
		private void ServerApplyStartingValues(bool resetPlayers)
		{
			// The machine is built in Awake, and the one path that skips it is the duplicate standing
			// down above. NGO spawns a scene object whether or not its component is enabled, so this
			// has to be asked here too rather than trusted to the disable.
			if (!IsServer || !IsSpawned || _stageMachine == null) return;

			foreach (var player in PokerPlayer.All)
			{
				if (!player || !player.Data) continue;

				var firstTime = !player.Data.HasConfiguredStartingStats;

				player.Data.ServerSetStartingHealth(_startingHealth);

				if (firstTime || (resetPlayers && _data && _data.Phase.Value == PokerPhase.Waiting))
				{
					player.Data.ServerResetForMatch();

					// The record of what they have already swallowed belongs to the match too, and it lives on
					// its own controller — reaching across from the data class to clear it would be a second
					// place to keep in step.
					if (player.BetItemConsume) player.BetItemConsume.ServerResetForMatch();
				}
			}
		}

		public void StartGame()
		{
			if (!IsServer) return;
			if (IsGameRunning) return;
			if (!CanStartMatch) return;

			foreach (var module in _modules)
			{
				if (module && !module.CanStartGame()) return;
			}

			// Who the match is being played by, decided once here. Everybody in a chair right now and able to
			// be dealt in is collected; anybody who sits down after this watches it out. The host who pressed
			// start while unconscious is not collected either — they hold the button, not a hand.
			// Between rounds of one match the stamps are still up; only the match reset takes them down.
			var firstRound = true;
			var playing = 0;
			foreach (var player in _seatedPlayers)
			{
				if (!player || !player.Data) continue;

				if (player.Data.InMatch.Value) firstRound = false;
				player.Data.InMatch.Value = CanBeDealtIn(player.Data);
				if (player.Data.InMatch.Value) playing++;
			}

			if (firstRound && _notices) _notices.ServerAnnounce(PokerNotice.ForMatchStarted(playing));

			foreach (var module in _modules)
			{
				if (module) module.OnGameStarted();
			}

			NextStage();
		}

		public void ResetGame()
		{
			if (!IsServer) return;

			GoToStage(0);
		}

		// Between two hands of the same match. Everything a hand leaves behind goes back — the cards, the
		// turn, whatever the modules were holding for it — but the phase is deliberately left alone: the
		// table is still mid-match, and a beat of Finished would unlock the chairs and read on every screen
		// as the match having ended.
		public void EndHand()
		{
			if (!IsServer) return;

			foreach (var module in _modules)
			{
				if (module) module.OnHandEnded();
			}

			ServerClearHands();
			ClearTurn();
		}

		public void EndGame()
		{
			if (!IsServer) return;

			EndHand();

			// After the hand, so a module tearing down its match state is doing it over a table that has
			// already put the hand away.
			foreach (var module in _modules)
			{
				if (module) module.OnMatchEnded();
			}

			// Who took the last hand of a finished match has no claim on the first hand of the next one.
			_data.LastWinnerClientId.Value = PokerGameData.NoTurn;

			_data.Phase.Value = PokerPhase.Finished;
		}

		// Blood and hallucination are what a match is played with, so putting them back is what makes the next
		// one a new match rather than a continuation. Every registered player, not only the seated: whoever
		// left their chair mid-match is still carrying whatever the match did to them.
		public void ServerResetMatchStats()
		{
			if (!IsServer) return;

			foreach (var player in PokerPlayer.All)
			{
				if (!player) continue;

				if (player.Data) player.Data.ServerResetForMatch();
				if (player.BetItemConsume) player.BetItemConsume.ServerResetForMatch();
			}
		}

		// The match is over, so the cards leave everyone's hands here — at the one point every ending
		// passes through — rather than trusting whichever stage happens to run next to tidy up. Every
		// registered player, not just the seated ones: whoever left their seat mid-hand walked off with
		// their cards, and no stage's reset would ever reach them again.
		private void ServerClearHands()
		{
			foreach (var player in PokerPlayer.All)
			{
				if (player && player.Data && player.Data.CardCount > 0) player.Data.HoleCards.Clear();
			}

			if (_data.CommunityCards.Count > 0) _data.CommunityCards.Clear();
			_data.RevealedCommunityMask.Value = 0;
			_streetTurnedCount = 0;
		}

		// Laid face down in one go, so a street only ever turns over what is already lying there.
		public void ServerDealCommunityCards(IReadOnlyList<CardData> cards)
		{
			if (!IsServer) return;

			_data.RevealedCommunityMask.Value = 0;
			_streetTurnedCount = 0;
			if (_data.CommunityCards.Count > 0) _data.CommunityCards.Clear();

			foreach (var card in cards) _data.CommunityCards.Add(card);
		}

		// Server only: how far along the board the streets have turned. An item turning a card does not move
		// it, because the flop is always the first three places, the turn the fourth and the river the fifth.
		private int _streetTurnedCount;

		// A street turns its own places, whatever an item turned before it; a place already face up stays so.
		public void ServerRevealCommunityCards(int count)
		{
			if (!IsServer || count <= 0) return;

			var mask = _data.RevealedCommunityMask.Value;
			var end = Mathf.Min(_streetTurnedCount + count, Mathf.Min(_data.CommunityCards.Count, 31));

			for (var i = _streetTurnedCount; i < end; i++) mask |= 1 << i;

			_streetTurnedCount = end;
			_data.RevealedCommunityMask.Value = mask;
		}

		public void ServerRevealCommunityCard(int slot)
		{
			if (!IsServer || slot < 0 || slot >= _data.CommunityCards.Count || slot >= 31) return;

			_data.RevealedCommunityMask.Value |= 1 << slot;
		}

		public void ServerRevealAllCommunityCards()
		{
			if (!IsServer) return;

			_streetTurnedCount = Mathf.Min(_data.CommunityCards.Count, 31);
			_data.RevealedCommunityMask.Value = (1 << _streetTurnedCount) - 1;
		}

		// One slot written in place, never a clear and refill, which every screen would play as a new deal.
		public void ServerReplaceCommunityCard(int slot, CardData card)
		{
			if (!IsServer || slot < 0 || slot >= _data.CommunityCards.Count) return;

			_data.CommunityCards[slot] = card;
		}

		// Somebody who went under mid-hand is out of it: their cards go face down as a fold's do and their
		// stake settles as a folder's, whatever the rules say about folding. The turn is handed on if it was
		// theirs, by the same path as a player leaving the table.
		public void ServerFoldOutOfHand(PokerPlayer player)
		{
			if (!IsServer || !player || !player.Data || !player.Data.IsInHand) return;

			player.ServerFold();

			if (_data.CurrentTurnClientId.Value == player.ClientId && CurrentStage)
				CurrentStage.HandlePlayerLeft(player.ClientId, player.Data.SeatIndex.Value);
		}

		// Who opens this hand: every street starts from them and a tie at the showdown is broken toward them.
		// Last hand's winner if they were dealt in, else the next player dealt in after their chair, else — the
		// first hand of a match — anybody dealt in, at random. Server-only: nothing on a client asks it.
		public ulong HandOpenerClientId { get; private set; } = PokerGameData.NoTurn;

		public void ServerChooseHandOpener()
		{
			if (!IsServer) return;

			HandOpenerClientId = PokerGameData.NoTurn;

			var winner = FindSeatedPlayer(_data.LastWinnerClientId.Value);
			if (winner && winner.Data.IsInHand)
			{
				HandOpenerClientId = winner.ClientId;
				return;
			}

			if (winner)
			{
				var next = PokerTableUtility.NextPlayer(_seatedPlayers, winner.Data.SeatIndex.Value, player => player.Data.IsInHand);
				if (next) HandOpenerClientId = next.ClientId;
				return;
			}

			var dealt = 0;
			foreach (var player in _seatedPlayers)
			{
				if (player && player.Data.IsInHand) dealt++;
			}

			if (dealt == 0) return;

			var pick = UnityEngine.Random.Range(0, dealt);
			foreach (var player in _seatedPlayers)
			{
				if (!player || !player.Data.IsInHand) continue;
				if (pick-- > 0) continue;

				HandOpenerClientId = player.ClientId;
				return;
			}
		}

		// The seat a walk starts *after*, so NextPlayer lands on the opener first. NoSeat with no opener,
		// which walks from the first chair.
		public int SeatBeforeHandOpener()
		{
			var opener = FindSeatedPlayer(HandOpenerClientId);
			if (!opener) return PokerPlayerData.NoSeat;

			var seat = opener.Data.SeatIndex.Value;
			if (seat < 0) return PokerPlayerData.NoSeat;

			var seatCount = Mathf.Max(1, _data.ActiveSeatCount.Value);
			return (seat - 1 + seatCount) % seatCount;
		}

		// Transitions are the server's alone; how they play out is the machine's business.
		public void NextStage()
		{
			if (!IsServer) return;

			_stageMachine.Next();
		}

		public void GoToStage(int index)
		{
			if (!IsServer) return;

			_stageMachine.GoTo(index);
		}

		public void GoToStage(PokerStage stage)
		{
			if (!IsServer) return;

			_stageMachine.GoTo(stage);
		}

		public void InsertStage(PokerStage stage)
		{
			if (!IsServer) return;

			_stageMachine.Insert(stage);
		}

		private void NotifyStageStarting(PokerStage stage)
		{
			if (!stage) return;

			foreach (var module in _modules)
			{
				if (module) module.OnStageStarting(stage);
			}
		}

		private void NotifyStageStarted(PokerStage stage)
		{
			if (!stage) return;

			foreach (var module in _modules)
			{
				if (module) module.OnStageStarted(stage);
			}

			OnStageChanged?.Invoke(stage);
		}

		private void NotifyStageEnded(PokerStage stage)
		{
			if (!stage) return;

			foreach (var module in _modules)
			{
				if (module) module.OnStageEnded(stage);
			}
		}

		public bool CanLeaveSeat(ulong clientId)
		{
			var player = FindSeatedPlayer(clientId);
			if (IsGameRunning && player && player.Data && IsCommittedToMatch(player.Data)) return false;

			foreach (var module in _modules)
			{
				if (module && !module.CanLeaveSeat(clientId)) return false;
			}

			return true;
		}

		// Being collected into a match commits the player to the match, not to the hand: folding is a
		// decision about these cards, and standing up afterwards would make folding a way out of the game.
		// What releases them is being out of the running — the same test the deal uses — so somebody who
		// has gone under may go, and nobody else may. Never dealt in at all is the other way out: a player
		// who took a free chair mid hand is Waiting and was never collected.
		private bool IsCommittedToMatch(PokerPlayerData data)
		{
			if (data.IsInHand) return true;

			if (data.Status.Value == PokerPlayerStatus.Waiting) return false;

			return CanBeDealtIn(data);
		}

		public void HandleSeatOccupied(PokerSeat seat, ulong clientId)
		{
			if (!IsServer || !seat) return;

			var player = PokerPlayer.Find(clientId);
			if (!player || !player.Data) return;

			// Sitting down costs nothing and hands out nothing.
			player.Data.ServerTakeSeat(seat.SeatIndex);
			RefreshSeatedPlayers();

			foreach (var module in _modules)
			{
				if (module) module.OnPlayerSeated(clientId, seat.SeatIndex);
			}
		}

		public void HandleSeatReleased(PokerSeat seat, ulong clientId)
		{
			if (!IsServer) return;

			var player = PokerPlayer.Find(clientId);
			if (player && player.Data)
			{
				if (IsGameRunning && player.Data.IsInHand)
				{
					player.ServerFold();

					// The cards go back with the seat: an unseated player is outside every stage's reset
					// sweep, and would otherwise carry the hand around for the rest of the session.
					player.Data.HoleCards.Clear();
				}
				else
				{
					player.Data.ServerLeaveSeat();
				}
			}

			RefreshSeatedPlayers();

			if (CurrentStage) CurrentStage.HandlePlayerLeft(clientId, seat ? seat.SeatIndex : -1);

			foreach (var module in _modules)
			{
				if (module) module.OnPlayerLeftSeat(clientId);
			}
		}

		private void HandleClientDisconnected(ulong clientId)
		{
			if (!IsServer) return;

			// Read off the seat, not the player: by now their object may already be despawned, and the
			// stage still needs to know which place at the table just emptied.
			var seatIndex = -1;
			foreach (var seat in _seats)
			{
				if (seat && seat.OccupantClientId == clientId) seatIndex = seat.SeatIndex;
			}

			foreach (var seat in _seats)
			{
				if (seat) seat.ReleaseIfOccupiedBy(clientId);
			}

			RefreshSeatedPlayers();

			// Clearing the turn is not enough on its own — a street waiting on a player who has gone
			// waits forever, and the table freezes for everyone still in it.
			if (CurrentStage) CurrentStage.HandlePlayerLeft(clientId, seatIndex);
		}

		public void BeginTurn(ulong clientId, float duration)
		{
			if (!IsServer) return;

			_data.CurrentTurnClientId.Value = clientId;
			_data.TurnDuration.Value = duration;
			_data.TurnEndTime.Value = NetworkManager.ServerTime.Time + duration;

			// A turn is also the commonest reason the table is looking at somebody, so every street gets the
			// focus for nothing. A beat that gives no turn and is still about one player says so itself.
			ServerSetFocus(clientId);

			// After the turn is on the table rather than before it: a module changing what this player is
			// carrying is answering a question they can already see being asked.
			foreach (var module in _modules)
			{
				if (module) module.OnTurnBegan(clientId);
			}
		}

		public void ClearTurn()
		{
			if (!IsServer) return;

			_data.CurrentTurnClientId.Value = PokerGameData.NoTurn;
			_data.TurnDuration.Value = 0f;
			_data.TurnEndTime.Value = 0d;

			ServerSetFocus(PokerGameData.NoTurn);
		}

		// Who every head in the room should be pointed at. Set alongside the turn wherever there is one, and
		// on its own by a beat that is about a player without asking them anything — the eating walks the
		// seats one at a time and gives out no turns at all.
		public void ServerSetFocus(ulong clientId)
		{
			if (!IsServer || !_data) return;

			_data.FocusClientId.Value = clientId;
		}

		public bool IsTurnExpired()
		{
			if (!_data.HasTurn || _data.TurnDuration.Value <= 0f) return false;

			return NetworkManager.ServerTime.Time >= _data.TurnEndTime.Value;
		}

		// The stage's own clock, running alongside the turn rather than instead of it — a stage that
		// plays out on its own, or one the whole table answers at once.
		public void BeginStageTimer(float duration)
		{
			if (!IsServer) return;

			_data.StageDuration.Value = Mathf.Max(0f, duration);
			_data.StageEndTime.Value = duration > 0f ? NetworkManager.ServerTime.Time + duration : 0d;
		}

		public void ClearStageTimer()
		{
			if (!IsServer) return;

			_data.StageDuration.Value = 0f;
			_data.StageEndTime.Value = 0d;
		}

		public bool IsStageTimerExpired()
		{
			if (!_data.HasStageTimer) return false;

			return NetworkManager.ServerTime.Time >= _data.StageEndTime.Value;
		}

		[Rpc(SendTo.Server)]
		public void RequestStartGameRPC(RpcParams rpcParams = default)
		{
			// Only the host starts the table, and only from a seat — the button is theirs alone.
			var senderClientId = rpcParams.Receive.SenderClientId;
			if (senderClientId != NetworkManager.ServerClientId) return;
			if (!FindSeatedPlayer(senderClientId)) return;

			StartGame();
		}

		[Rpc(SendTo.Server)]
		public void SubmitActionRPC(PokerActionType action, int amount, RpcParams rpcParams = default)
		{
			var senderClientId = rpcParams.Receive.SenderClientId;

			foreach (var module in _modules)
			{
				if (module && !module.CanPlayerAct(senderClientId, action, amount)) return;
			}

			if (!CurrentStage || !CurrentStage.HandleAction(senderClientId, action, amount)) return;

			// A stage whose answers are sealed tells nobody who answered what.
			if (CurrentStage.AnnouncesActions && _notices) _notices.ServerAnnounce(PokerNotice.ForAction(senderClientId, action));

			foreach (var module in _modules)
			{
				if (module) module.OnPlayerActed(senderClientId, action, amount);
			}
		}

		[Rpc(SendTo.Server)]
		public void SubmitModuleCommandRPC(FixedString32Bytes commandId, int payload, RpcParams rpcParams = default)
		{
			var senderClientId = rpcParams.Receive.SenderClientId;

			foreach (var module in _modules)
			{
				if (module && module.HandleCommandServer(senderClientId, commandId, payload)) return;
			}
		}
	}
}
