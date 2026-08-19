using System;
using System.Collections.Generic;
using Game.Runtime.GameMode.Poker.Hands;
using Game.Runtime.GameMode.Poker.Modules;
using Game.Runtime.GameMode.Poker.Player;
using Game.Runtime.GameMode.Poker.Stages;
using Game.Runtime.UI;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker
{
	public class PokerGameMode : NetworkBehaviour, IGameMode
	{
		[Header("Rules")]
		[SerializeField] private PokerRuleSettings _rules;

		[Header("Stages")]
		[Tooltip("The round loop as a preset. Swap this asset to change the game — modules still add to it, and any stage can be interrupted at runtime by InsertStage or PushOverlay.")]
		[SerializeField] private PokerStageSequence _sequence;

		[Header("Modules")]
		[Tooltip("Plugged in features — each one may add stages, commands and restrictions of its own.")]
		[SerializeField] private List<PokerModule> _modules = new();

		[Header("UI")]
		[Tooltip("Spawned locally on every peer while the mode is alive. Presentation only, so it is a plain prefab and never travels over the network.")]
		[SerializeField] private GameObject _hudPrefab;

		[Header("References")] 
		[SerializeField] private PokerGameData _data;
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
		public PokerRuleSettings Rules => _rules;
		public PokerStageSequence Sequence => _sequence;
		public PokerDeck Deck { get; } = new();
		public PokerHandEvaluator HandEvaluator { get; } = new();

		public IReadOnlyList<PokerSeat> Seats => _seats;
		public IReadOnlyList<PokerModule> Modules => _modules;
		public IReadOnlyList<PokerStage> Stages => _stageMachine.Stages;

		// Seated players in seat order — the order the turn passes around the table.
		public IReadOnlyList<PokerPlayer> SeatedPlayers => _seatedPlayers;

		// Sitting down is free, so a seat filled is not the same as a player who can play. Only the ones
		// still breathing and with money left to stake make a hand worth dealing.
		public int FundedPlayerCount
		{
			get
			{
				var count = 0;

				foreach (var player in _seatedPlayers)
				{
					if (player.Data.IsAlive && player.Data.Chips > 0) count++;
				}

				return count;
			}
		}

		public PokerStage CurrentStage => _stageMachine?.CurrentStage;
		public PokerStage CurrentOverlay => _stageMachine?.CurrentOverlay;
		public PokerStage ActiveStage => _stageMachine?.ActiveStage;

		public bool IsGameRunning => _data && _data.Phase.Value != PokerPhase.Waiting && _data.Phase.Value != PokerPhase.Finished;

		// One press of start begins a match, and a match is however many hands the table can still deal.
		// Asked at the end of each one, because a hand is exactly what takes players out of the running:
		// the same count the host's button is gated on, so a table that could be started can be continued.
		public bool CanDealAnotherHand => _rules && FundedPlayerCount >= _rules.MinimumPlayersToStart;

		public event Action OnSeatedPlayersChanged;
		public event Action<PokerStage> OnStageChanged;

		private readonly List<PokerPlayer> _seatedPlayers = new();

		private PokerStageMachine _stageMachine;
		private GameObject _hudInstance;

		private void Awake()
		{
			if (Instance && Instance != this)
			{
				Destroy(gameObject);
				return;
			}

			Instance = this;

			_stageMachine = new PokerStageMachine(this, NotifyStageStarted, NotifyStageEnded);
		}

		public override void OnDestroy()
		{
			if (Instance == this) Instance = null;

			base.OnDestroy();
		}

		public override void OnNetworkSpawn()
		{
			if (!_data) _data = GetComponentInChildren<PokerGameData>();
			if (_seats.Count == 0) CollectRegisteredSeats();

			_stageMachine.Build(_sequence, _modules);

			foreach (var module in _modules)
			{
				if (module) module.Initialize(this);
			}

			_stageMachine.InitializeStages();

			PokerPlayer.OnRegistryChanged += RefreshSeatedPlayers;
			RefreshSeatedPlayers();

			OnInstanceChanged?.Invoke(this);

			SpawnHud();

			if (!IsServer) return;

			NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnected;
			GoToStage(0);
		}

		public override void OnNetworkDespawn()
		{
			PokerPlayer.OnRegistryChanged -= RefreshSeatedPlayers;

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
			if (!IsServer || !IsSpawned) return;

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

		public PokerStage ResolveRuntimeStage(PokerStage stage) => _stageMachine.Resolve(stage);

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

		public void RefreshSeatedPlayers()
		{
			_seatedPlayers.Clear();

			foreach (var player in PokerPlayer.All)
			{
				if (player && player.Data && player.Data.IsSeated) _seatedPlayers.Add(player);
			}

			_seatedPlayers.Sort((left, right) => left.Data.SeatIndex.Value.CompareTo(right.Data.SeatIndex.Value));
			OnSeatedPlayersChanged?.Invoke();
		}

		public PokerPlayer FindSeatedPlayer(ulong clientId)
		{
			foreach (var player in _seatedPlayers)
			{
				if (player.ClientId == clientId) return player;
			}

			return null;
		}

		public void StartGame()
		{
			if (!IsServer) return;
			if (IsGameRunning) return;
			if (FundedPlayerCount < _rules.MinimumPlayersToStart) return;

			foreach (var module in _modules)
			{
				if (module && !module.CanStartGame()) return;
			}

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
				if (module) module.OnGameEnded();
			}

			ServerClearHands();
			ClearTurn();
		}

		public void EndGame()
		{
			if (!IsServer) return;

			EndHand();

			_data.Phase.Value = PokerPhase.Finished;
		}

		// Blood and money are what a match is played with, so putting them back is what makes the next one
		// a new match rather than a continuation. Every registered player, not only the seated: whoever
		// left their chair mid-match is still carrying whatever the match did to them.
		public void ServerResetMatchStats()
		{
			if (!IsServer) return;

			foreach (var player in PokerPlayer.All)
			{
				if (player && player.Data) player.Data.ServerResetForMatch();
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

				// Cards swept away face down: a peek pose held over the next deal would show a lift with
				// nothing in it.
				if (player && player.HandPeek) player.HandPeek.ServerSetPeeking(false);
			}
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

		public void PushOverlay(PokerStage stage)
		{
			if (!IsServer) return;

			_stageMachine.PushOverlay(stage);
		}

		public void PopOverlay()
		{
			if (!IsServer) return;

			_stageMachine.PopOverlay();
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
			// Sitting down commits the player to the hand — they are only released once the table is
			// idle again, or if they were never dealt in.
			var player = FindSeatedPlayer(clientId);
			if (IsGameRunning && player && player.Data.IsInHand) return false;

			foreach (var module in _modules)
			{
				if (module && !module.CanLeaveSeat(clientId)) return false;
			}

			return true;
		}

		public void HandleSeatOccupied(PokerSeat seat, ulong clientId)
		{
			if (!IsServer || !seat) return;

			var player = PokerPlayer.Find(clientId);
			if (!player || !player.Data) return;

			// Sitting down costs nothing and hands out nothing: the player stakes the money they already
			// own, so there is no buy-in to take and no stack to grant.
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
					player.Data.Status.Value = PokerPlayerStatus.Folded;
					player.Data.HasActed.Value = true;

					// The chips they had in front of them stay behind as dead money — collected now,
					// because once their object despawns no street-end sweep will ever see them, and the
					// players who pushed them out would win back nothing but their own bets.
					_data.Pot.Value += player.Data.Bet.Value;
					player.Data.ServerCollectBet();

					// The cards go back with the seat: an unseated player is outside every stage's reset
					// sweep, and would otherwise carry the hand around for the rest of the session.
					player.Data.HoleCards.Clear();
				}
				else
				{
					// Nothing to settle: the money never left the wallet to begin with, so standing up is
					// only a matter of giving up the seat.
					player.Data.ServerLeaveSeat();
				}
			}

			RefreshSeatedPlayers();

			ActiveStage?.HandlePlayerLeft(clientId, seat ? seat.SeatIndex : -1);

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
			ActiveStage?.HandlePlayerLeft(clientId, seatIndex);
		}

		public void BeginTurn(ulong clientId, float duration)
		{
			if (!IsServer) return;

			_data.CurrentTurnClientId.Value = clientId;
			_data.TurnDuration.Value = duration;
			_data.TurnEndTime.Value = NetworkManager.ServerTime.Time + duration;

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

		// The whole board comes off the shuffle at the deal and goes down on the table there and then, so no
		// stage can change what the river will be after seeing how the betting went. It goes down face
		// down: the cards are all dealt, and how much of them the table has been shown is a separate thing
		// the streets move.
		public void ServerDealCommunityCards(List<CardData> cards)
		{
			if (!IsServer) return;

			_data.CommunityCards.Clear();
			foreach (var card in cards) _data.CommunityCards.Add(card);

			_data.RevealedCommunityCards.Value = 0;
		}

		// Turns the next few cards of the board over for the whole table. Nothing is dealt here — the cards
		// have been lying there since the deal, and this only says how many of them everyone may see.
		public void RevealCommunityCards(int amount)
		{
			if (!IsServer) return;

			var revealed = Mathf.Clamp(_data.RevealedCommunityCards.Value + amount, 0, _data.CommunityCards.Count);
			if (revealed == _data.RevealedCommunityCards.Value) return;

			_data.RevealedCommunityCards.Value = revealed;
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

			// Measured across the stage rather than trusted from the sender: the request says "call", but
			// what that actually cost is the stage's answer, and the announcement shows the real number.
			var actor = FindSeatedPlayer(senderClientId);
			var betBefore = actor ? actor.Data.Bet.Value : 0;

			if (ActiveStage == null || !ActiveStage.HandleAction(senderClientId, action, amount)) return;

			// An overlay prices its moves in its own currency and announces them itself. Measuring one here
			// would read the change in Bet, which an overlay never touches, and go out as a bare verb with
			// nothing after it — beside the overlay's own card saying the same thing properly.
			if (CurrentOverlay == null)
			{
				var paid = actor ? Mathf.Max(0, actor.Data.Bet.Value - betBefore) : 0;
				_data.ActionNotice.Value = new PokerActionNotice
				{
					ClientId = senderClientId,
					Action = action,
					Amount = paid,
					Sequence = _data.ActionNotice.Value.Sequence + 1
				};
			}

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
