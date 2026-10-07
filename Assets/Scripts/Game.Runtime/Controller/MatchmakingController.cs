using System;
using System.Collections.Generic;
using System.Threading;
using Game.Runtime.Lobby;
using Sirenix.OdinInspector;
using Unity.Netcode;
using UnityEngine;

namespace Game.Runtime.Controller
{
	// Quick match: joins the open matchmaking room with the lowest ping, or opens one, and waits there until
	// it is full. Nobody connects a session while they wait, so leaving costs nothing: an owner who cancels
	// hands the room to whoever has waited longest, and that member takes over starting it. Once the room
	// is full its owner hosts the session and marks the room started; everyone else connects on seeing it.
	// Anything that breaks along the way ends matchmaking with a failure the popup reports.
	public class MatchmakingController : MonoBehaviour
	{
		public static MatchmakingController Instance { get; private set; }

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void ResetStatics() => Instance = null;

		[Required]
		[SerializeField] private GameNetworkManager _network;

		[Required]
		[SerializeField] private NetworkManager _networkManager;

		[Tooltip("Players a matchmaking room holds; the match starts once it is full.")]
		[MinValue(2)]
		[SerializeField] private int _targetPlayers = 4;

		[Tooltip("Rooms tried, lowest ping first, before opening a new one.")]
		[MinValue(1)]
		[SerializeField] private int _maxJoinAttempts = 5;

		private readonly List<ILobby> _candidates = new();

		private ILobbyService _service;
		private ILobby _lobby;
		private CancellationTokenSource _search;

		// Set around the starts this controller makes, so its own OnConnectStarted is not taken for a way
		// in from elsewhere (an invite) that should end matchmaking.
		private bool _handingOff;

		// Between handing the room to GameNetworkManager and the local player being connected: a failure in
		// that stretch is still matchmaking's to report.
		private bool _awaitingSession;

		public event Action<MatchmakingState> OnStateChanged;
		public event Action OnMembersChanged;
		public event Action<MatchmakingFailure> OnFailed;

		public MatchmakingState State { get; private set; }
		public int TargetPlayers => _targetPlayers;
		public int MemberCount => _lobby?.MemberCount ?? 0;

		private bool IsOwner => _lobby != null && _service != null && _lobby.OwnerId == _service.LocalMemberId;

		private void Awake()
		{
			if (Instance && Instance != this)
			{
				Destroy(gameObject);
				return;
			}

			Instance = this;
		}

		private void Start()
		{
			if (Instance != this) return;

			_network.OnConnectStarted += HandleConnectStarted;
			_network.OnConnectFailed += HandleConnectFailed;
			_networkManager.OnClientConnectedCallback += HandleClientConnected;
		}

		private void OnDestroy()
		{
			if (Instance != this) return;

			if (_networkManager) _networkManager.OnClientConnectedCallback -= HandleClientConnected;

			if (_network)
			{
				_network.OnConnectFailed -= HandleConnectFailed;
				_network.OnConnectStarted -= HandleConnectStarted;
			}

			Abandon();
			Instance = null;
		}

		private void OnApplicationQuit() => Abandon();

		public void StartMatchmaking()
		{
			if (State != MatchmakingState.Idle || _network.IsInGame) return;

			_search = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
			SearchAsync(_search.Token).LogExceptionsAndForget();
		}

		// Allowed until the session is being opened; from then on the loading screen is up and leaving is
		// the pause menu's.
		public void CancelMatchmaking()
		{
			if (State is MatchmakingState.Idle or MatchmakingState.Starting) return;

			Abandon();
			SetState(MatchmakingState.Idle);
		}

		private async Awaitable SearchAsync(CancellationToken ct)
		{
			SetState(MatchmakingState.Searching);

			var service = _network.ResolveLobbyService();
			if (!service.IsAvailable)
			{
				Fail(MatchmakingFailure.Unavailable);
				return;
			}

			ILobby lobby;
			try
			{
				lobby = await JoinBestAsync(service, ct) ?? await CreateAsync(service, ct);
			}
			catch (OperationCanceledException)
			{
				return;
			}
			catch (Exception exception)
			{
				Debug.LogException(exception);
				Fail(MatchmakingFailure.ConnectFailed);
				return;
			}

			if (lobby == null)
			{
				Fail(MatchmakingFailure.CreateFailed);
				return;
			}

			Bind(service, lobby);
			SetState(MatchmakingState.Waiting);
			OnMembersChanged?.Invoke();
			Evaluate();
		}

		private List<LobbyData> SearchFilters()
		{
			var filters = new List<LobbyData>(_network.LobbySettings.GameSearchStrings)
			{
				new(LobbyConstant.GameModeKey, _network.LobbySettings.SelectedGameMode.ToString()),
				new(LobbyConstant.MatchmakingKey, LobbyConstant.MatchmakingValue),
				new(LobbyConstant.MatchStateKey, LobbyConstant.MatchStateGathering)
			};

			return filters;
		}

		// Lowest ping first; between rooms equally near, the one closest to full, so players gather into
		// fewer rooms rather than waiting apart. A room that filled or started in the meantime is left again.
		private async Awaitable<ILobby> JoinBestAsync(ILobbyService service, CancellationToken ct)
		{
			var found = await service.SearchAsync(SearchFilters(), ct);
			ct.ThrowIfCancellationRequested();

			_candidates.Clear();
			foreach (var lobby in found)
			{
				if (lobby.MemberCount < lobby.MaxMembers) _candidates.Add(lobby);
			}

			_candidates.Sort((a, b) =>
			{
				var byPing = PingRank(service, a).CompareTo(PingRank(service, b));
				return byPing != 0 ? byPing : (a.MaxMembers - a.MemberCount).CompareTo(b.MaxMembers - b.MemberCount);
			});

			var attempts = Mathf.Min(_maxJoinAttempts, _candidates.Count);
			for (var i = 0; i < attempts; i++)
			{
				var joined = await service.JoinAsync(_candidates[i].Id, ct);

				if (ct.IsCancellationRequested)
				{
					if (joined != null) service.Leave(joined);
					ct.ThrowIfCancellationRequested();
				}

				if (joined == null) continue;
				if (joined.GetData(LobbyConstant.MatchStateKey) == LobbyConstant.MatchStateGathering) return joined;

				service.Leave(joined);
			}

			return null;
		}

		private static int PingRank(ILobbyService service, ILobby lobby)
		{
			var ping = service.EstimatePing(lobby);
			return ping < 0 ? int.MaxValue : ping;
		}

		private async Awaitable<ILobby> CreateAsync(ILobbyService service, CancellationToken ct)
		{
			var data = _network.BuildLobbyData(service);
			data.Add(new LobbyData(LobbyConstant.MatchmakingKey, LobbyConstant.MatchmakingValue));
			data.Add(new LobbyData(LobbyConstant.MatchStateKey, LobbyConstant.MatchStateGathering));

			var lobby = await service.CreateAsync(new LobbyCreateRequest(_targetPlayers, false, data), ct);

			if (ct.IsCancellationRequested)
			{
				if (lobby != null) service.Leave(lobby);
				ct.ThrowIfCancellationRequested();
			}

			return lobby;
		}

		private void Bind(ILobbyService service, ILobby lobby)
		{
			_service = service;
			_lobby = lobby;

			_service.OnLobbyChanged += HandleLobbyChanged;
			_service.OnLobbyLost += HandleLobbyLost;
		}

		private void Unbind()
		{
			if (_service != null)
			{
				_service.OnLobbyLost -= HandleLobbyLost;
				_service.OnLobbyChanged -= HandleLobbyChanged;
			}

			_service = null;
			_lobby = null;
		}

		private void HandleLobbyChanged(ILobby lobby)
		{
			if (_lobby == null || lobby.Id != _lobby.Id) return;

			_lobby = lobby;
			OnMembersChanged?.Invoke();
			Evaluate();
		}

		private void HandleLobbyLost(ILobby lobby)
		{
			if (_lobby == null || lobby.Id != _lobby.Id) return;

			Fail(MatchmakingFailure.LobbyLost);
		}

		// Asked on every change, so a member who has just been handed the room starts it if it is already full.
		private void Evaluate()
		{
			if (State != MatchmakingState.Waiting || _lobby == null) return;

			if (IsOwner)
			{
				if (_lobby.MemberCount >= _targetPlayers) OpenSession();
			}
			else if (_lobby.GetData(LobbyConstant.MatchStateKey) == LobbyConstant.MatchStateStarted)
			{
				JoinSession();
			}
		}

		// The room is closed to searches before the host goes up, and marked started only once it is up, so
		// a member never connects to a host that is not listening yet.
		private void OpenSession()
		{
			var service = _service;
			var lobby = _lobby;

			SetState(MatchmakingState.Starting);
			service.SetJoinable(lobby, false);
			Unbind();

			_awaitingSession = true;
			_handingOff = true;
			var hosted = _network.StartHostInLobby(service, lobby);
			_handingOff = false;

			if (hosted) service.SetData(lobby, LobbyConstant.MatchStateKey, LobbyConstant.MatchStateStarted);
		}

		private void JoinSession()
		{
			var service = _service;
			var lobby = _lobby;

			SetState(MatchmakingState.Starting);
			Unbind();

			_awaitingSession = true;
			_handingOff = true;
			_network.JoinSessionInLobby(service, lobby);
			_handingOff = false;
		}

		private void HandleClientConnected(ulong clientId)
		{
			if (!_awaitingSession || clientId != _networkManager.LocalClientId) return;

			_awaitingSession = false;
			SetState(MatchmakingState.Idle);
		}

		private void HandleConnectFailed(string reason)
		{
			if (!_awaitingSession) return;

			Debug.LogWarning($"[MatchmakingController] The match could not be joined: {reason}");
			Fail(MatchmakingFailure.ConnectFailed);
		}

		// A way into a table taken from elsewhere while waiting (an invite) wins over matchmaking.
		private void HandleConnectStarted()
		{
			if (_handingOff || State is MatchmakingState.Idle or MatchmakingState.Starting) return;

			Abandon();
			SetState(MatchmakingState.Idle);
		}

		private void Fail(MatchmakingFailure failure)
		{
			Abandon();
			SetState(MatchmakingState.Idle);
			OnFailed?.Invoke(failure);
		}

		// An owner leaving with others still waiting hands the room on first, so the wait goes on for them.
		private void Abandon()
		{
			_awaitingSession = false;

			_search?.Cancel();
			_search?.Dispose();
			_search = null;

			if (_lobby == null) return;

			var service = _service;
			var lobby = _lobby;
			Unbind();

			if (lobby.OwnerId == service.LocalMemberId) service.TransferOwnership(lobby);
			service.Leave(lobby);
		}

		private void SetState(MatchmakingState state)
		{
			if (State == state) return;

			State = state;
			OnStateChanged?.Invoke(state);
		}
	}
}
