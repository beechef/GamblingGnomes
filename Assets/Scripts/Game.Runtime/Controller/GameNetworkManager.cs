using System;
using System.Collections.Generic;
using System.Threading;
using Game.Runtime.GameMode;
using Game.Runtime.Lobby;
using Game.Runtime.Steam;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

namespace Game.Runtime.Controller
{
	public class GameNetworkManager : MonoBehaviour
	{
		public static GameNetworkManager Instance { get; private set; }

		// Domain Reload is disabled, so statics survive between play sessions.
		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void ResetStatics()
		{
			Instance = null;
		}

		[Header("Game Network Settings")]
		[field: SerializeField] public LobbySettings LobbySettings { get; private set; } = new(
			6,
			false,
			GameModeType.Main,
			new List<LobbyData>(),
			new List<LobbyData>()
		);

		[SerializeField] private GameModeDatabase _gameModeDatabase;

		[Header("Lobby")]
		[Tooltip("Which lobby the editor opens and searches. Local needs neither Steam nor the internet: rooms are files in the temp folder, seen by every editor, Multiplayer Play Mode player and build on this machine. A build always tries Steam first.")]
		[SerializeField] private LobbyBackend _editorLobby = LobbyBackend.Local;

		[Tooltip("When the preferred lobby is Steam and Steam is not signed in (offline, not running), open and search local rooms instead of failing.")]
		[SerializeField] private bool _fallBackToLocal = true;

		[Header("References")]
		[SerializeField] private NetworkManager _networkManager;

		[SerializeField] private FacepunchTransport _steamTransport;

		[FormerlySerializedAs("_editorTransport")]
		[SerializeField] private UnityTransport _localTransport;

		public event Action<string> OnConnectFailed;
		public event Action OnLobbyEnter;
		public event Action OnHostStarted;

		// Raised the moment a way into a table is taken, before the lobby is asked anything. The answer can be
		// seconds away, and OnHostStarted/OnLobbyEnter only arrive once it comes — so this is the edge
		// anything covering the wait has to start from.
		public event Action OnConnectStarted;

		// The mirror of it on the way out: the teardown below unloads a scene, so the table is gone well
		// before the app is back to a blank state.
		public event Action OnGameLeaving;

		// Raised once the table is fully torn down and the app is back to a blank state — whether the
		// player walked out, the host vanished, or the transport died.
		public event Action OnGameLeft;

		public ILobby CurrentLobby { get; private set; }

		public bool IsInGame => CurrentLobby != null || _networkManager.IsListening;

		private SteamLobbyService _steamLobby;
		private LocalLobbyService _localLobby;

		// The service the current room came from; leaving and inviting go back through it.
		private ILobbyService _lobbyService;

		private bool _joiningLobby;
		private bool _leavingGame;
		private Scene _gameplayScene;
		private string _gameplaySceneName;

		private void Awake()
		{
			if (Instance != null && Instance != this)
			{
				Destroy(gameObject);
				return;
			}
			Instance = this;
			DontDestroyOnLoad(gameObject);

			_steamLobby = new SteamLobbyService(_steamTransport, _localTransport);
			_localLobby = new LocalLobbyService(_localTransport);

			LobbySettings.GameSearchStrings.Add(new LobbyData(LobbyConstant.GameIDKey, LobbyConstant.GameIDValue));
		}

		private void OnEnable()
		{
			_networkManager.OnClientConnectedCallback += OnClientConnected;
			_networkManager.OnClientDisconnectCallback += OnClientDisconnected;
			_networkManager.OnTransportFailure += OnTransportFailure;
			_networkManager.OnServerStopped += OnServerStopped;
		}

		private void OnDisable()
		{
			_networkManager.OnClientConnectedCallback -= OnClientConnected;
			_networkManager.OnClientDisconnectCallback -= OnClientDisconnected;
			_networkManager.OnTransportFailure -= OnTransportFailure;
			_networkManager.OnServerStopped -= OnServerStopped;
		}

		// Steam binds its callbacks once SteamClient.Init has run, which happens in the same scene in no
		// guaranteed order — the service waits for it itself, so this only has to start it after Awake.
		private void Start()
		{
			if (Instance != this) return;

			_steamLobby.OnInviteAccepted += AcceptInvite;
			_steamLobby.Initialize();
			_localLobby.Initialize();
		}

		private void OnDestroy()
		{
			if (Instance != this) return;

			_steamLobby.OnInviteAccepted -= AcceptInvite;
			_steamLobby.Dispose();
			_localLobby.Dispose();
		}

		// Asked at every host, join and search rather than once: Steam signs in after the menu is up, and can
		// drop out (offline) between one room and the next.
		public ILobbyService ResolveLobbyService()
		{
			var preferSteam = !Application.isEditor || _editorLobby == LobbyBackend.Steam;
			if (!preferSteam || _steamLobby.IsAvailable) return preferSteam ? _steamLobby : _localLobby;

			if (!_fallBackToLocal) return _steamLobby;

			Debug.LogWarning("[GameNetworkManager] Steam is not signed in; using local rooms on this machine instead.");
			return _localLobby;
		}

		public void ConfigureLobby(int maxPlayers, bool isPrivate, GameModeType gameMode)
		{
			var settings = LobbySettings;
			settings.MaxPlayers = maxPlayers;
			settings.IsPrivate = isPrivate;
			settings.SelectedGameMode = gameMode;
			LobbySettings = settings;
		}

		public bool TryGetSelectedGameMode(out GameModeDatabase.GameModeEntry entry) =>
			_gameModeDatabase.TryGetEntry(LobbySettings.SelectedGameMode, out entry);

		public async Awaitable StartHost(CancellationToken ct = default)
		{
			ct.ThrowIfCancellationRequested();

			var service = ResolveLobbyService();

			// Reported rather than only logged: every start has to end in an outcome the UI hears, or
			// whatever covered the wait is left up with nothing coming to take it down.
			if (!service.IsAvailable)
			{
				OnConnectFailed?.Invoke($"{service.Name} lobby is not available.");
				return;
			}

			if (!TryGetSelectedGameMode(out var entry))
			{
				OnConnectFailed?.Invoke($"No GameModeDatabase entry for mode {LobbySettings.SelectedGameMode}.");
				return;
			}

			_gameplaySceneName = entry.SceneName;

			// Raised past the guards, so the only starts announced are the ones that go on to answer.
			OnConnectStarted?.Invoke();

			var request = new LobbyCreateRequest(LobbySettings.MaxPlayers, LobbySettings.IsPrivate, BuildLobbyData(service));
			var lobby = await service.CreateAsync(request, ct);

			// A lobby cannot call a request already in flight back, and the room is up by the time this
			// returns — so a caller that gave up gets it handed back rather than left hosting a table nobody
			// is going to walk into.
			if (ct.IsCancellationRequested)
			{
				if (lobby != null) service.Leave(lobby);
				ct.ThrowIfCancellationRequested();
			}

			if (lobby == null)
			{
				OnConnectFailed?.Invoke($"Failed to create a {service.Name} lobby.");
				return;
			}

			HostLobby(service, lobby);
		}

		// Opens the session for a room this player already owns, gathered elsewhere (matchmaking). True once
		// the host is up; a refusal is reported through OnConnectFailed like any other start.
		public bool StartHostInLobby(ILobbyService service, ILobby lobby)
		{
			if (!TryGetSelectedGameMode(out var entry))
			{
				OnConnectFailed?.Invoke($"No GameModeDatabase entry for mode {LobbySettings.SelectedGameMode}.");
				return false;
			}

			_gameplaySceneName = entry.SceneName;

			OnConnectStarted?.Invoke();
			return HostLobby(service, lobby);
		}

		private bool HostLobby(ILobbyService service, ILobby lobby)
		{
			CurrentLobby = lobby;
			_lobbyService = service;

			if (_networkManager.IsConnectedClient) _networkManager.Shutdown();
			_networkManager.NetworkConfig.NetworkTransport = service.BindTransport(lobby, true);

			if (!_networkManager.StartHost())
			{
				OnConnectFailed?.Invoke("NetworkManager.StartHost() failed.");
				LeaveLobby(false);
				return false;
			}

			_networkManager.SceneManager.LoadScene(_gameplaySceneName, LoadSceneMode.Additive);
			StartListenGameplaySceneLoad();

			OnHostStarted?.Invoke();
			OnLobbyEnter?.Invoke();
			return true;
		}

		// Connects to the session the owner of a room this player is already in has opened (matchmaking).
		public void JoinSessionInLobby(ILobbyService service, ILobby lobby)
		{
			if (CurrentLobby != null || _joiningLobby)
			{
				OnConnectFailed?.Invoke("Already joining or in a room.");
				return;
			}

			OnConnectStarted?.Invoke();
			EnterAsClient(service, lobby);
		}

		public List<LobbyData> BuildLobbyData(ILobbyService service)
		{
			var data = new List<LobbyData>
			{
				new(LobbyConstant.RoomNameKey, service.LocalUserName),
				new(LobbyConstant.GameModeKey, LobbySettings.SelectedGameMode.ToString())
			};

			data.AddRange(LobbySettings.GameSearchStrings);
			data.AddRange(LobbySettings.LobbyData);
			return data;
		}

		// A room from the list is joined through the same service the list came from.
		public Awaitable JoinLobby(ILobby lobby, CancellationToken ct = default) =>
			JoinLobby(ResolveLobbyService(), lobby.Id, ct);

		private async Awaitable JoinLobby(ILobbyService service, ulong lobbyId, CancellationToken ct = default)
		{
			if (_joiningLobby) return;
			if (CurrentLobby != null) return;

			_joiningLobby = true;

			// Released on every path, cancel included: left standing it would turn every later join
			// into a silent no-op.
			try
			{
				ct.ThrowIfCancellationRequested();

				OnConnectStarted?.Invoke();

				var lobby = await service.JoinAsync(lobbyId, ct);

				if (ct.IsCancellationRequested)
				{
					if (lobby != null) service.Leave(lobby);
					ct.ThrowIfCancellationRequested();
				}

				if (lobby == null)
				{
					OnConnectFailed?.Invoke($"Failed to join the {service.Name} lobby (timeout, full or gone).");
					return;
				}

				EnterAsClient(service, lobby);
			}
			finally
			{
				_joiningLobby = false;
			}
		}

		private void EnterAsClient(ILobbyService service, ILobby lobby)
		{
			CurrentLobby = lobby;
			_lobbyService = service;
			OnLobbyEnter?.Invoke();

			var gameModeString = lobby.GetData(LobbyConstant.GameModeKey);
			if (Enum.TryParse<GameModeType>(gameModeString, out var gameMode) &&
				_gameModeDatabase.TryGetEntry(gameMode, out var entry))
			{
				_gameplaySceneName = entry.SceneName;
			}

			_networkManager.NetworkConfig.NetworkTransport = service.BindTransport(lobby, false);

			if (!_networkManager.StartClient())
			{
				OnConnectFailed?.Invoke("NetworkManager.StartClient() failed.");
				LeaveLobby(false);
				return;
			}

			StartListenGameplaySceneLoad();
		}

		public async Awaitable<IReadOnlyList<ILobby>> SearchLobby(CancellationToken ct = default)
		{
			ct.ThrowIfCancellationRequested();

			var lobbies = await ResolveLobbyService().SearchAsync(LobbySettings.GameSearchStrings, ct);

			ct.ThrowIfCancellationRequested();

			if (lobbies == null) return Array.Empty<ILobby>();

			// A matchmaking room is only entered through quick match: joined by hand, nobody in it would wait
			// for the room to fill or hear that it started.
			var hosted = new List<ILobby>(lobbies.Count);
			foreach (var lobby in lobbies)
			{
				if (lobby.GetData(LobbyConstant.MatchmakingKey) != LobbyConstant.MatchmakingValue) hosted.Add(lobby);
			}

			return hosted;
		}

		// Whether this player has a room friends can be asked into.
		public bool CanInviteFriends => CurrentLobby != null && _lobbyService != null && _lobbyService.CanInvite(CurrentLobby);

		// Opens the lobby's own friend picker on the current room; whoever accepts lands in AcceptInvite on their side.
		public void InviteFriends()
		{
			if (!CanInviteFriends)
			{
				Debug.LogWarning("[GameNetworkManager] Invite refused: this room has no invites.");
				return;
			}

			_lobbyService.OpenInviteDialog(CurrentLobby);
		}

		// Every way an invite arrives ends here: launched by it, accepted in the menu, or accepted while sitting
		// in another room. Only the latest one counts, and one arriving while a join or leave is still running
		// waits for it rather than being dropped, since the player has no way to send it again. Invites only
		// come from Steam, so they are joined through it.
		public void AcceptInvite(ulong lobbyId)
		{
			if (CurrentLobby != null && CurrentLobby.Id == lobbyId) return;

			_pendingInvite = lobbyId;
			if (_acceptingInvite) return;

			AcceptPendingInvitesAsync(destroyCancellationToken).LogExceptionsAndForget();
		}

		private ulong? _pendingInvite;
		private bool _acceptingInvite;

		private async Awaitable AcceptPendingInvitesAsync(CancellationToken ct)
		{
			_acceptingInvite = true;

			try
			{
				// One frame first: an invite read off the command line arrives from Start, and the menu that hides
				// itself on OnConnectStarted may not have subscribed yet.
				await Awaitable.NextFrameAsync(ct);

				while (_pendingInvite.HasValue)
				{
					// A join or a leave already under way finishes first; tearing into it halfway leaves the
					// transport and the scene out of step.
					while (_joiningLobby || _leavingGame) await Awaitable.NextFrameAsync(ct);

					var lobbyId = _pendingInvite.Value;
					_pendingInvite = null;

					if (CurrentLobby != null && CurrentLobby.Id == lobbyId) continue;

					// Sitting in another room: walk out of it the normal way first, so the next room starts
					// from the same blank state a join from the menu does.
					if (IsInGame) await LeaveGame(ct);

					// A newer invite arrived while leaving: that one is the one the player meant.
					if (_pendingInvite.HasValue) continue;

					await JoinLobby(_steamLobby, lobbyId, ct);
				}
			}
			finally
			{
				_acceptingInvite = false;
			}
		}

		private void OnClientConnected(ulong clientId)
		{
			if (clientId != _networkManager.LocalClientId) return;
			Debug.Log("[GameNetworkManager] Connected to server successfully.");
		}

		private void OnClientDisconnected(ulong clientId)
		{
			if (clientId != _networkManager.LocalClientId) return;

			// Shutting down is how leaving works, so the callbacks it raises are our own footsteps —
			// reporting them as a failed connection would put an error on screen for a clean exit.
			if (_leavingGame) return;

			var reason = _networkManager.DisconnectReason;
			if (string.IsNullOrEmpty(reason)) reason = "Connection lost or rejected (no reason provided).";

			Debug.Log($"[GameNetworkManager] Disconnected from server: {reason}");

			Shutdown();
			OnConnectFailed?.Invoke(reason);
		}

		private void OnServerStopped(bool isStopped)
		{
			if (_leavingGame) return;

			Debug.Log("[GameNetworkManager] Server stopped.");
			Shutdown();
		}

		private void OnTransportFailure()
		{
			if (_leavingGame) return;

			Shutdown();

			OnConnectFailed?.Invoke("Transport-level failure (NAT/relay/socket error).");
		}

		// The single way out, whatever the reason. It runs to completion even when there is no lobby or
		// no connection left to close, because half a teardown is what stops the next host from starting:
		// everything it touches ends up back where Awake left it.
		public async Awaitable LeaveGame(CancellationToken ct = default)
		{
			if (_leavingGame) return;
			_leavingGame = true;

			// Announced only when there is a session to take apart: Shutdown is called on paths where
			// nothing is up, and those would otherwise put a screen over a teardown that does nothing.
			if (IsInGame) OnGameLeaving?.Invoke();

			try
			{
				StopListenGameplaySceneLoad();

				LeaveLobby(Application.isEditor);

				if (_networkManager.IsListening) _networkManager.Shutdown();

				// Cancelling stops the waiting, never the teardown: everything this method changed is
				// still put back below, because half a teardown is what stops the next host from starting.
				await UnloadGameplayScene(ct);
			}
			finally
			{
				_gameplaySceneName = null;
				_joiningLobby = false;
				_leavingGame = false;
			}

			OnGameLeft?.Invoke();
		}

		// The gameplay scene comes in additively through the network scene manager, but it outlives the
		// shutdown that just happened — so it goes out through the plain one.
		private async Awaitable UnloadGameplayScene(CancellationToken ct = default)
		{
			if (!_gameplayScene.IsValid() || !_gameplayScene.isLoaded)
			{
				_gameplayScene = default;
				return;
			}

			var unload = SceneManager.UnloadSceneAsync(_gameplayScene);
			_gameplayScene = default;

			// Awaited rather than fired off: hosting again immediately would otherwise load the next
			// gameplay scene on top of one still being torn down.
			while (unload != null && !unload.isDone) await Awaitable.NextFrameAsync(ct);
		}

		public void Shutdown()
		{
			LeaveGame().LogExceptionsAndForget();
		}

		private void OnApplicationQuit()
		{
			// No point unloading a scene the process is about to drop — just hand the lobby back.
			LeaveLobby(false);

			if (_networkManager.IsListening) _networkManager.Shutdown();
		}

		private void LeaveLobby(bool hostOnly)
		{
			if (CurrentLobby == null) return;

			// Two editor instances share one Steam lobby, so only the host may hand it back — a client leaving
			// would close the room out from under the player still hosting it.
			// Local rooms are files per process and have no such sharing.
			var mayLeave = !hostOnly || _networkManager.IsHost || _lobbyService != _steamLobby;

			if (mayLeave) _lobbyService?.Leave(CurrentLobby);

			CurrentLobby = null;
			_lobbyService = null;
		}

		private void StartListenGameplaySceneLoad()
		{
			if (_networkManager.SceneManager != null)
				_networkManager.SceneManager.OnLoadComplete += OnLoadSceneComplete;
		}

		private void StopListenGameplaySceneLoad()
		{
			if (_networkManager.SceneManager != null)
				_networkManager.SceneManager.OnLoadComplete -= OnLoadSceneComplete;
		}

		private void OnLoadSceneComplete(ulong clientId, string sceneName, LoadSceneMode loadMode)
		{
			if (sceneName == _gameplaySceneName)
			{
				_gameplayScene = SceneManager.GetSceneByName(sceneName);
			}
		}
	}
}
