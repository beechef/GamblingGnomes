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

		[SerializeField] private List<PlayerSpawnPoint> _spawnPoints = new();

		public NetworkList<NetworkObjectReference> Players { get; } =
			new(writePerm: NetworkVariableWritePermission.Server, readPerm: NetworkVariableReadPermission.Everyone);

		private readonly Dictionary<ulong, NetworkObject> _playerObjects = new();

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
			if (_spawnPoints.Count == 0)
			{
				Debug.LogWarning("No spawn points assigned — players will spawn at the origin.");
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

			var spawnPoint = GetRandomSpawnPoint();
			var player = NetworkManager.SpawnManager.InstantiateAndSpawn(_playerPrefab,
				ownerClientId: clientId,
				isPlayerObject: true,
				position: spawnPoint ? spawnPoint.transform.position : Vector3.zero,
				rotation: spawnPoint ? spawnPoint.transform.rotation : Quaternion.identity);

			Players.Add(player);
			_playerObjects[clientId] = player;
		}

		private void HandlePlayerDisconnected(ulong clientId)
		{
			if (!IsHost) return;

			if (_playerObjects.TryGetValue(clientId, out var player))
			{
				Players.Remove(player);

				_playerObjects.Remove(clientId);

				player.Despawn();
			}
		}

		private void HandlePreShutdown()
		{
			if (!IsHost) return;

			foreach (var player in _playerObjects.Values)
			{
				player?.Despawn();
			}

			_playerObjects.Clear();
			Players.Clear();
		}

		private PlayerSpawnPoint GetRandomSpawnPoint()
		{
			return _spawnPoints.Count == 0 ? null : _spawnPoints[UnityEngine.Random.Range(0, _spawnPoints.Count)];
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
