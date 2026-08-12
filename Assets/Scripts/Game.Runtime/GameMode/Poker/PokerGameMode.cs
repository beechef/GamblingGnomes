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
		[Tooltip("The round loop, in order. Modules add to it, and any stage can be interrupted at runtime by InsertStage or PushOverlay.")]
		[SerializeField] private List<PokerStage> _stages = new();

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

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void ResetStatics() => Instance = null;

		public PokerGameData Data => _data;
		public PokerRuleSettings Rules => _rules;
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

		private readonly List<PokerStage> _runtimeStages = new();
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
			if (_seats.Count == 0) CollectSceneSeats();

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

			SpawnHud();

			if (!IsServer) return;

			NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnected;
			GoToStage(0);
		}

		public override void OnNetworkDespawn()
		{
			PokerPlayer.OnRegistryChanged -= RefreshSeatedPlayers;

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

		private void BuildRuntimeStages()
		{
			_runtimeStages.Clear();

			foreach (var stage in _stages)
			{
				if (stage) _runtimeStages.Add(stage);
			}

			foreach (var module in _modules)
			{
				if (module) module.CollectStages(_runtimeStages);
			}
		}

		private void CollectSceneSeats()
		{
			_seats.Clear();
			_seats.AddRange(FindObjectsByType<PokerSeat>());
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
			var index = _runtimeStages.IndexOf(stage);
			if (index >= 0) GoToStage(index);
		}

		// Queued to run as the next stage, after whatever is running now finishes on its own terms.
		public void InsertStage(PokerStage stage)
		{
			if (!IsServer || !stage) return;

			_insertedStages.Enqueue(stage);
		}

		// Runs immediately on top, freezing everything below it until it is popped.
		public void PushOverlay(PokerStage stage)
		{
			if (!IsServer || !stage) return;

			ActiveStage?.PauseStage();

			_overlayStack.Add(stage);
			_data.OverlayStageId.Value = stage.StageId;

			stage.StartStage();
			NotifyStageStarted(stage);
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
