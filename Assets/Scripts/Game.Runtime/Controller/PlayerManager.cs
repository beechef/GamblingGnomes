using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Game.Runtime.Controller
{
	public class PlayerManager : NetworkBehaviour
	{
		public event Action<NetworkObject> OnPlayerSpawned;
		public event Action<NetworkObject> OnPlayerDespawned;

		[SerializeField] private NetworkObject _playerPrefab;

		[SerializeField] private List<PlayerSpawnPoint> _sortedClockwiseSpawnPoints = new();
		[SerializeField] private List<PlayerSpawnPoint> _sequentialSpawnPoints = new();

		private readonly Dictionary<ulong, PlayerSpawnPoint> _playerSpawnPoints = new();

		public NetworkList<NetworkObjectReference> Players { get; } =
			new(writePerm: NetworkVariableWritePermission.Server, readPerm: NetworkVariableReadPermission.Everyone);

		private readonly Dictionary<ulong, NetworkObject> _playerObjects = new();

		public List<NetworkObject> SortedPlayers
		{
			get
			{
				var sortedPlayers = new List<NetworkObject>();
				foreach (var spawnPoint in _sortedClockwiseSpawnPoints)
				{
					if (spawnPoint.Player.TryGet(out var player))
					{
						sortedPlayers.Add(player);
					}
				}

				return sortedPlayers;
			}
		}


		public override void OnNetworkSpawn()
		{
			Players.OnListChanged += HandlePlayersChanged;

			NetworkManager.Singleton.OnClientConnectedCallback += HandleNewPlayer;
			NetworkManager.Singleton.OnClientDisconnectCallback += HandlePlayerDisconnected;
			NetworkManager.Singleton.OnPreShutdown += HandlePreShutdown;

			base.OnNetworkSpawn();
		}

		protected override void OnNetworkPostSpawn()
		{
			if (_sortedClockwiseSpawnPoints.Count != _sequentialSpawnPoints.Count)
			{
				Debug.LogWarning("The number of sorted spawn points does not match the number of sequential spawn points.");
			}

			var clients = NetworkManager.Singleton.ConnectedClients.Values;
			foreach (var client in clients)
			{
				HandleNewPlayer(client.ClientId);
			}

			base.OnNetworkPostSpawn();
		}

		public override void OnNetworkDespawn()
		{
			Players.OnListChanged -= HandlePlayersChanged;

			NetworkManager.Singleton.OnClientConnectedCallback -= HandleNewPlayer;
			NetworkManager.Singleton.OnClientDisconnectCallback -= HandlePlayerDisconnected;
			NetworkManager.Singleton.OnPreShutdown -= HandlePreShutdown;

			base.OnNetworkDespawn();
		}

		private void HandleNewPlayer(ulong clientId)
		{
			if (!IsHost) return;

			var spawnPoint = FindAvailableSpawnPoint();
			var player = NetworkManager.SpawnManager.InstantiateAndSpawn(_playerPrefab,
				ownerClientId: clientId,
				isPlayerObject: true,
				position: spawnPoint.transform.position,
				rotation: spawnPoint.transform.rotation);

			spawnPoint.SetPlayer(player);

			Players.Add(player);
			_playerObjects[clientId] = player;
			_playerSpawnPoints[clientId] = spawnPoint;
		}

		private void HandlePlayerDisconnected(ulong clientId)
		{
			if (!IsHost) return;

			var spawnPoint = FindPlayerSpawnPoint(clientId);
			if (spawnPoint) spawnPoint.ClearPlayer();

			if (_playerObjects.TryGetValue(clientId, out var player))
			{
				Players.Remove(player);

				_playerObjects.Remove(clientId);

				player.Despawn();
			}

			_playerSpawnPoints.Remove(clientId);
		}

		private void HandlePreShutdown()
		{
			if (!IsHost) return;

			foreach (var player in _playerObjects.Values)
			{
				player.Despawn();
			}

			_playerObjects.Clear();
			_playerSpawnPoints.Clear();
			Players.Clear();
		}

		private PlayerSpawnPoint FindAvailableSpawnPoint()
		{
			foreach (var spawnPoint in _sequentialSpawnPoints)
			{
				if (spawnPoint.IsValid) continue;

				return spawnPoint;
			}

			return null;
		}

		private PlayerSpawnPoint FindPlayerSpawnPoint(ulong clientId)
		{
			return _playerSpawnPoints.GetValueOrDefault(clientId);
		}

		private void HandlePlayersChanged(NetworkListEvent<NetworkObjectReference> changeEvent)
		{
			switch (changeEvent.Type)
			{
				case NetworkListEvent<NetworkObjectReference>.EventType.Add:
				{
					var playerRef = changeEvent.Value;
					if (playerRef.TryGet(out var player))
					{
						OnPlayerSpawned?.Invoke(player);
					}
					break;
				}

				case NetworkListEvent<NetworkObjectReference>.EventType.Remove:
				{
					var playerRef = changeEvent.Value;
					if (playerRef.TryGet(out var player))
					{
						OnPlayerDespawned?.Invoke(player);
					}
					break;
				}
			}
		}
	}
}
