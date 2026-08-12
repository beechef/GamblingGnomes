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
		public IReadOnlyList<PokerStage> Stages => _runtimeStages;

		// Seated players in seat order — the order the turn passes around the table.
		public IReadOnlyList<PokerPlayer> SeatedPlayers => _seatedPlayers;

		public PokerStage CurrentStage { get; private set; }
		public PokerStage CurrentOverlay => _overlayStack.Count > 0 ? _overlayStack[^1] : null;
		public PokerStage ActiveStage => CurrentOverlay ? CurrentOverlay : CurrentStage;

		public bool IsGameRunning => _data && _data.Phase.Value != PokerPhase.Waiting && _data.Phase.Value != PokerPhase.Finished;

		public event Action OnSeatedPlayersChanged;
		public event Action<PokerStage> OnStageChanged;

		private readonly List<PokerStage> _stageSources = new();
		private readonly List<PokerStage> _runtimeStages = new();

		// Stages reached by reference rather than by sequence — a module's overlay, a hand over branch.
		// They run the same way, they just never take a slot in the loop.
		private readonly List<PokerStage> _detachedStages = new();

		private readonly List<PokerStage> _overlayStack = new();
		private readonly Queue<PokerStage> _insertedStages = new();
		private readonly List<PokerPlayer> _seatedPlayers = new();
		private readonly List<CardData> _pendingCommunityCards = new();

		private GameObject _hudInstance;
		private int _nextStageIndex;

		private void Awake()
		{
			if (Instance && Instance != this)
			{
				Destroy(gameObject);
				return;
			}

			Instance = this;
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

			BuildRuntimeStages();

			foreach (var module in _modules)
			{
				if (module) module.Initialize(this);
			}

			foreach (var stage in _runtimeStages)
			{
				if (stage) stage.Initialize(this);
			}

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

			CurrentStage?.EndStage();
			for (var i = _overlayStack.Count - 1; i >= 0; i--) _overlayStack[i].EndStage();
			_overlayStack.Clear();
			_insertedStages.Clear();

			foreach (var stage in _runtimeStages)
			{
				if (stage) stage.DeInitialize();
			}

			foreach (var stage in _detachedStages)
			{
				if (stage) stage.DeInitialize();
			}

			ReleaseRuntimeStages();

			foreach (var module in _modules)
			{
				if (module) module.DeInitialize();
			}
		}

		private void Update()
		{
			if (!IsServer || !IsSpawned) return;

			ActiveStage?.TickStage(Time.deltaTime);
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

		// Assets describe the round; the mode plays clones of them, so nothing a stage does at runtime is
		// written back into the project.
		private void BuildRuntimeStages()
		{
			ReleaseRuntimeStages();

			_stageSources.Clear();

			if (_sequence) _sequence.CollectStages(_stageSources);

			foreach (var module in _modules)
			{
				if (module) module.CollectStages(_stageSources);
			}

			foreach (var source in _stageSources)
			{
				if (source) _runtimeStages.Add(CreateRuntimeStage(source));
			}
		}

		private PokerStage CreateRuntimeStage(PokerStage source)
		{
			var runtime = Instantiate(source);
			runtime.name = source.name;

			return runtime;
		}

		private void ReleaseRuntimeStages()
		{
			foreach (var stage in _runtimeStages)
			{
				if (stage) Destroy(stage);
			}

			foreach (var stage in _detachedStages)
			{
				if (stage) Destroy(stage);
			}

			_runtimeStages.Clear();
			_detachedStages.Clear();

			CurrentStage = null;
			_nextStageIndex = 0;
		}

		public PokerStage FindStage(string stageId)
		{
			if (string.IsNullOrEmpty(stageId)) return null;

			foreach (var stage in _runtimeStages)
			{
				if (stage && stage.StageId == stageId) return stage;
			}

			foreach (var stage in _detachedStages)
			{
				if (stage && stage.StageId == stageId) return stage;
			}

			return null;
		}

		// Stage fields point at project assets, so a reference has to be traded for the clone actually
		// running. Anything the sequence never mentions gets a clone of its own on first use.
		public PokerStage ResolveRuntimeStage(PokerStage stage)
		{
			if (!stage) return null;

			var running = FindStage(stage.StageId);
			if (running) return running;

			var runtime = CreateRuntimeStage(stage);
			_detachedStages.Add(runtime);
			runtime.Initialize(this);

			return runtime;
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
			if (_seatedPlayers.Count < _rules.MinimumPlayersToStart) return;

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

		public void EndGame()
		{
			if (!IsServer) return;

			foreach (var module in _modules)
			{
				if (module) module.OnGameEnded();
			}

			_data.Phase.Value = PokerPhase.Finished;
			ClearTurn();
		}

		public void NextStage()
		{
			if (!IsServer) return;

			CurrentStage?.EndStage();
			NotifyStageEnded(CurrentStage);

			// An inserted stage runs before the sequence resumes, and deliberately does not consume a
			// slot — when it ends the round picks up exactly where it left off.
			if (_insertedStages.Count > 0)
			{
				SetCurrentStage(_insertedStages.Dequeue());
				return;
			}

			if (_nextStageIndex >= _runtimeStages.Count) _nextStageIndex = 0;

			var stage = _runtimeStages.Count > 0 ? _runtimeStages[_nextStageIndex] : null;
			_nextStageIndex++;

			SetCurrentStage(stage);
		}

		public void GoToStage(int index)
		{
			if (!IsServer) return;
			if (_runtimeStages.Count == 0) return;

			CurrentStage?.EndStage();
			NotifyStageEnded(CurrentStage);

			_nextStageIndex = Mathf.Clamp(index, 0, _runtimeStages.Count - 1);
			var stage = _runtimeStages[_nextStageIndex];
			_nextStageIndex++;

			SetCurrentStage(stage);
		}

		public void GoToStage(PokerStage stage)
		{
			var index = _runtimeStages.IndexOf(ResolveRuntimeStage(stage));
			if (index >= 0) GoToStage(index);
		}

		// Queued to run as the next stage, after whatever is running now finishes on its own terms.
		public void InsertStage(PokerStage stage)
		{
			if (!IsServer || !stage) return;

			_insertedStages.Enqueue(ResolveRuntimeStage(stage));
		}

		// Runs immediately on top, freezing everything below it until it is popped.
		public void PushOverlay(PokerStage stage)
		{
			if (!IsServer || !stage) return;

			var overlay = ResolveRuntimeStage(stage);
			if (!overlay) return;

			ActiveStage?.PauseStage();

			_overlayStack.Add(overlay);
			_data.OverlayStageId.Value = overlay.StageId;

			overlay.StartStage();
			NotifyStageStarted(overlay);
		}

		public void PopOverlay()
		{
			if (!IsServer || _overlayStack.Count == 0) return;

			var overlay = _overlayStack[^1];
			_overlayStack.RemoveAt(_overlayStack.Count - 1);

			overlay.EndStage();
			NotifyStageEnded(overlay);

			_data.OverlayStageId.Value = CurrentOverlay ? CurrentOverlay.StageId : string.Empty;
			ActiveStage?.ResumeStage();
		}

		private void SetCurrentStage(PokerStage stage)
		{
			CurrentStage = stage;
			_data.StageId.Value = stage ? stage.StageId : string.Empty;

			// The clock belongs to whoever is running, so it never survives the handover.
			ClearStageTimer();

			if (!stage) return;

			stage.StartStage();
			NotifyStageStarted(stage);
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

			player.Data.ServerTakeSeat(seat.SeatIndex, _rules.StartingChips);
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
				}
				else
				{
					player.Data.ServerLeaveSeat();
				}
			}

			RefreshSeatedPlayers();

			foreach (var module in _modules)
			{
				if (module) module.OnPlayerLeftSeat(clientId);
			}
		}

		private void HandleClientDisconnected(ulong clientId)
		{
			if (!IsServer) return;

			foreach (var seat in _seats)
			{
				if (seat) seat.ReleaseIfOccupiedBy(clientId);
			}

			RefreshSeatedPlayers();

			if (_data.CurrentTurnClientId.Value == clientId) ClearTurn();
		}

		public void BeginTurn(ulong clientId, float duration)
		{
			if (!IsServer) return;

			_data.CurrentTurnClientId.Value = clientId;
			_data.TurnDuration.Value = duration;
			_data.TurnEndTime.Value = NetworkManager.ServerTime.Time + duration;
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

		// The board is dealt face down at shuffle time and revealed a street at a time, so no stage can
		// change what the river will be after seeing how the betting went.
		public void ServerSetPendingCommunityCards(List<CardData> cards)
		{
			if (!IsServer) return;

			_pendingCommunityCards.Clear();
			_pendingCommunityCards.AddRange(cards);
		}

		public void RevealCommunityCards(int amount)
		{
			if (!IsServer) return;

			for (var i = 0; i < amount; i++)
			{
				var next = _data.CommunityCards.Count;
				if (next >= _pendingCommunityCards.Count) return;

				_data.CommunityCards.Add(_pendingCommunityCards[next]);
			}
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

			if (ActiveStage == null || !ActiveStage.HandleAction(senderClientId, action, amount)) return;

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
