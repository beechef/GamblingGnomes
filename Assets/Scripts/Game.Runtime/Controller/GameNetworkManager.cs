using System;
using System.Collections.Generic;
using System.Threading;
using Game.Runtime.GameMode;
using Game.Runtime.Steam;
using Steamworks;
using Steamworks.Data;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

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
			GameModeType.Sandbox,
			new List<LobbyData>(),
			new List<LobbyData>()
		);

		[SerializeField] private GameModeDatabase _gameModeDatabase;

		[Header("References")]
		[SerializeField] private NetworkManager _networkManager;

		[SerializeField] private FacepunchTransport _steamTransport;
		[SerializeField] private NetworkTransport _editorTransport;

		public event Action<string> OnConnectFailed;
		public event Action OnLobbyEnter;
		public event Action OnHostStarted;

		// Raised the moment a way into a table is taken, before Steam is asked anything. The answer can be
		// seconds away, and OnHostStarted/OnLobbyEnter only arrive once it comes — so this is the edge
		// anything covering the wait has to start from.
		public event Action OnConnectStarted;

		// The mirror of it on the way out: the teardown below unloads a scene, so the table is gone well
		// before the app is back to a blank state.
		public event Action OnGameLeaving;

		// Raised once the table is fully torn down and the app is back to a blank state — whether the
		// player walked out, the host vanished, or the transport died.
		public event Action OnGameLeft;

		public Lobby? CurrentLobby { get; private set; }

		public bool IsInGame => CurrentLobby.HasValue || _networkManager.IsListening;

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

			if (Application.isEditor)
			{
				_networkManager.NetworkConfig.NetworkTransport = _editorTransport;
			}
			else
			{
				_networkManager.NetworkConfig.NetworkTransport = _steamTransport;
			}

			LobbySettings.GameSearchStrings.Add(new LobbyData(LobbyConstant.GameIDKey, LobbyConstant.GameIDValue));
		}

		private void OnEnable()
		{
			SteamMatchmaking.OnLobbyCreated += OnLobbyCreated;
			SteamMatchmaking.OnLobbyEntered += OnLobbyEntered;
			SteamMatchmaking.OnLobbyMemberJoined += OnLobbyMemberJoined;
			SteamMatchmaking.OnLobbyMemberLeave += OnLobbyMemberLeave;
			SteamFriends.OnGameLobbyJoinRequested += OnGameLobbyJoinRequested;

			_networkManager.OnClientConnectedCallback += OnClientConnected;
			_networkManager.OnClientDisconnectCallback += OnClientDisconnected;
			_networkManager.OnTransportFailure += OnTransportFailure;
			_networkManager.OnServerStopped += OnServerStopped;
		}

		private void OnDisable()
		{
			SteamMatchmaking.OnLobbyCreated -= OnLobbyCreated;
			SteamMatchmaking.OnLobbyEntered -= OnLobbyEntered;
			SteamMatchmaking.OnLobbyMemberJoined -= OnLobbyMemberJoined;
			SteamMatchmaking.OnLobbyMemberLeave -= OnLobbyMemberLeave;
			SteamFriends.OnGameLobbyJoinRequested -= OnGameLobbyJoinRequested;

			_networkManager.OnClientConnectedCallback -= OnClientConnected;
			_networkManager.OnClientDisconnectCallback -= OnClientDisconnected;
			_networkManager.OnTransportFailure -= OnTransportFailure;
			_networkManager.OnServerStopped -= OnServerStopped;
		}

		public void ConfigureLobby(int maxPlayers, bool isPrivate, GameModeType gameMode)
		{
			var settings = LobbySettings;
			settings.MaxPlayers = maxPlayers;
			settings.IsPrivate = isPrivate;
			settings.SelectedGameMode = gameMode;
			LobbySettings = settings;
		}

		public async Awaitable StartHost(CancellationToken ct = default)
		{
			ct.ThrowIfCancellationRequested();

			if (!SteamClient.IsValid)
			{
				// Reported rather than only logged: every start has to end in an outcome the UI hears, or
				// whatever covered the wait is left up with nothing coming to take it down.
				OnConnectFailed?.Invoke("SteamClient invalid before CreateLobbyAsync.");
				return;
			}

			if (!_gameModeDatabase.TryGetEntry(LobbySettings.SelectedGameMode, out var entry))
			{
				OnConnectFailed?.Invoke($"No GameModeDatabase entry for mode {LobbySettings.SelectedGameMode}.");
				return;
			}

			_gameplaySceneName = entry.SceneName;

			// Raised past the guards, so the only starts announced are the ones that go on to answer.
			OnConnectStarted?.Invoke();

			var result = await SteamMatchmaking.CreateLobbyAsync(LobbySettings.MaxPlayers);

			// Steam has no way to call a request already in flight back, and the room is up by the time
			// this returns — so a caller that gave up gets it handed back rather than left hosting a
			// table nobody is going to walk into.
			if (ct.IsCancellationRequested)
			{
				if (result.HasValue) Shutdown();
				ct.ThrowIfCancellationRequested();
			}

			if (!result.HasValue)
			{
				OnConnectFailed?.Invoke("Failed to create Steam lobby.");
			}
		}

		private void OnLobbyCreated(Result result, Lobby lobby)
		{
			if (result != Result.OK)
			{
				OnConnectFailed?.Invoke($"Lobby creation failed: {result}");
				return;
			}

			if (LobbySettings.IsPrivate)
			{
				lobby.SetPrivate();
			}
			else
			{
				lobby.SetPublic();
			}

			lobby.SetJoinable(true);
			lobby.SetData(LobbyConstant.RoomNameKey, SteamClient.Name);
			lobby.SetData(LobbyConstant.GameModeKey, LobbySettings.SelectedGameMode.ToString());

			foreach (var searchKeyPair in LobbySettings.GameSearchStrings)
			{
				lobby.SetData(searchKeyPair.Key, searchKeyPair.Value);
			}

			foreach (var kvp in LobbySettings.LobbyData)
			{
				lobby.SetData(kvp.Key, kvp.Value);
			}

			CurrentLobby = lobby;

			_steamTransport.TargetSteamId = SteamClient.SteamId.Value;

			if (_networkManager.IsConnectedClient) _networkManager.Shutdown();
			var started = _networkManager.StartHost();
			if (!started)
			{
				OnConnectFailed?.Invoke("NetworkManager.StartHost() failed.");
				lobby.Leave();
				CurrentLobby = null;
				return;
			}

			_networkManager.SceneManager.LoadScene(_gameplaySceneName, LoadSceneMode.Additive);
			StartListenGameplaySceneLoad();

			OnHostStarted?.Invoke();
		}

		public async Awaitable JoinLobby(Lobby lobby, CancellationToken ct = default)
		{
			await JoinLobby(lobby.Id, ct);
		}

		public async Awaitable JoinLobby(SteamId lobbyId, CancellationToken ct = default)
		{
			if (_joiningLobby) return;
			if (CurrentLobby.HasValue) return;

			_joiningLobby = true;

			// Released on every path, cancel included: left standing it would turn every later join
			// into a silent no-op.
			try
			{
				ct.ThrowIfCancellationRequested();

				OnConnectStarted?.Invoke();

				var lobby = await SteamMatchmaking.JoinLobbyAsync(lobbyId);

				if (ct.IsCancellationRequested)
				{
					if (lobby.HasValue) Shutdown();
					ct.ThrowIfCancellationRequested();
				}

				if (!lobby.HasValue)
				{
					OnConnectFailed?.Invoke("Failed to join Steam lobby (timeout or invalid lobby).");
				}
			}
			finally
			{
				_joiningLobby = false;
			}
		}

		public async Awaitable<Lobby[]> SearchLobby(CancellationToken ct = default)
		{
			ct.ThrowIfCancellationRequested();

			var lobbyQuery = SteamMatchmaking.LobbyList;

			foreach (var searchKeyPair in LobbySettings.GameSearchStrings)
			{
				lobbyQuery.WithKeyValue(searchKeyPair.Key, searchKeyPair.Value);
			}

			var lobbies = lobbyQuery.RequestAsync();
			var result = await lobbies;

			ct.ThrowIfCancellationRequested();

			return result ?? Array.Empty<Lobby>();
		}

		private void OnGameLobbyJoinRequested(Lobby lobby, SteamId friendId)
		{
			JoinLobby(lobby).LogExceptionsAndForget();
		}

		private void OnLobbyEntered(Lobby lobby)
		{
			CurrentLobby = lobby;
			OnLobbyEnter?.Invoke();

			if (_networkManager.IsHost) return;

			var gameModeString = lobby.GetData(LobbyConstant.GameModeKey);
			if (Enum.TryParse<GameModeType>(gameModeString, out var gameMode) &&
				_gameModeDatabase.TryGetEntry(gameMode, out var entry))
			{
				_gameplaySceneName = entry.SceneName;
			}

			_steamTransport.TargetSteamId = lobby.Owner.Id;

			var started = _networkManager.StartClient();
			if (!started)
			{
				OnConnectFailed?.Invoke("NetworkManager.StartClient() failed.");
				lobby.Leave();
				CurrentLobby = null;
				return;
			}

			StartListenGameplaySceneLoad();
		}

		private void OnLobbyMemberJoined(Lobby lobby, Friend friend)
		{
			Debug.Log($"[GameNetworkManager] {friend.Name} joined lobby.");
		}

		private void OnLobbyMemberLeave(Lobby lobby, Friend friend)
		{
			Debug.Log($"[GameNetworkManager] {friend.Name} left lobby.");
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
			if (!CurrentLobby.HasValue) return;

			// Two editor instances share one lobby, so only the host may hand it back — a client leaving
			// would close the room out from under the player still hosting it.
			var mayLeave = !hostOnly || _networkManager.IsHost;

			// Steam can already be down by the time this runs: nothing orders one OnApplicationQuit
			// against another, and calling into a shut down client throws from inside Facepunch.
			if (mayLeave && SteamClient.IsValid) CurrentLobby.Value.Leave();

			CurrentLobby = null;
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
