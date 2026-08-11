using System;
using System.Collections.Generic;
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

		public Lobby? CurrentLobby { get; private set; }

		private bool _joiningLobby;
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

		public async Awaitable StartHost()
		{
			if (!SteamClient.IsValid)
			{
				Debug.LogError("[GameNetworkManager] SteamClient invalid before CreateLobbyAsync");
				return;
			}

			if (!_gameModeDatabase.TryGetEntry(LobbySettings.SelectedGameMode, out var entry))
			{
				OnConnectFailed?.Invoke($"No GameModeDatabase entry for mode {LobbySettings.SelectedGameMode}.");
				return;
			}

			_gameplaySceneName = entry.SceneName;

			var result = await SteamMatchmaking.CreateLobbyAsync(LobbySettings.MaxPlayers);
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

		public async Awaitable JoinLobby(Lobby lobby)
		{
			await JoinLobby(lobby.Id);
		}

		public async Awaitable JoinLobby(SteamId lobbyId)
		{
			if (_joiningLobby) return;
			if (CurrentLobby.HasValue) return;
			_joiningLobby = true;

			var lobby = await SteamMatchmaking.JoinLobbyAsync(lobbyId);
			if (!lobby.HasValue)
			{
				OnConnectFailed?.Invoke("Failed to join Steam lobby (timeout or invalid lobby).");
			}

			_joiningLobby = false;
		}

		public async Awaitable<Lobby[]> SearchLobby()
		{
			var lobbyQuery = SteamMatchmaking.LobbyList;

			foreach (var searchKeyPair in LobbySettings.GameSearchStrings)
			{
				lobbyQuery.WithKeyValue(searchKeyPair.Key, searchKeyPair.Value);
			}

			var lobbies = lobbyQuery.RequestAsync();
			var result = await lobbies;

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

			var reason = _networkManager.DisconnectReason;
			if (string.IsNullOrEmpty(reason)) reason = "Connection lost or rejected (no reason provided).";

			Debug.Log($"[GameNetworkManager] Disconnected from server: {reason}");

			Shutdown();
			OnConnectFailed?.Invoke(reason);
		}

		private void OnServerStopped(bool isStopped)
		{
			Debug.Log("[GameNetworkManager] Server stopped.");
			Shutdown();
		}

		private void OnTransportFailure()
		{
			Shutdown();

			OnConnectFailed?.Invoke("Transport-level failure (NAT/relay/socket error).");
		}

		private void LeaveGame()
		{
			if (CurrentLobby.HasValue)
			{
				if (Application.isEditor)
				{
					if (_networkManager.IsHost)
					{
						CurrentLobby.Value.Leave();
					}
				}
				else
				{
					CurrentLobby.Value.Leave();
				}

				StopListenGameplaySceneLoad();

				CurrentLobby = null;
				_networkManager.Shutdown();

				if (_gameplayScene.IsValid())
				{
					SceneManager.UnloadSceneAsync(_gameplayScene);
				}
			}
		}

		public void Shutdown()
		{
			LeaveGame();
		}

		private void OnApplicationQuit()
		{
			Shutdown();
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
