using System;
using System.Collections.Generic;
using Game.Runtime.Player;
using Unity.Netcode;
using UnityEngine;

namespace Game.Runtime.Controller
{
	// Owns a player object from spawn to teardown. Netcode would otherwise take one down with the
	// connection that owned it, which leaves nothing to hand back when that player returns — the prefab
	// opts out of that, so a disconnect arrives here with the object still intact and every decision
	// about it made in one place.
	public class PlayerManager : NetworkBehaviour
	{
		public event Action<NetworkObject> OnPlayerSpawned;
		public event Action<NetworkObject> OnPlayerDespawned;

		[SerializeField] private NetworkObject _playerPrefab;

		[SerializeField] private List<PlayerSpawnPoint> _spawnPoints = new();

		public NetworkList<NetworkObjectReference> Players { get; } =
			new(writePerm: NetworkVariableWritePermission.Server, readPerm: NetworkVariableReadPermission.Everyone);

		// Keyed by connection, but each entry carries the identity that outlives it — matching a
		// returning player against that id is what reconnecting will be built on.
		private readonly Dictionary<ulong, PlayerEntry> _players = new();

		private readonly struct PlayerEntry
		{
			public PlayerEntry(ulong playerId, NetworkObject player)
			{
				PlayerId = playerId;
				NetworkObjectId = player.NetworkObjectId;
				Object = player;
			}

			public ulong PlayerId { get; }

			// Held beside the object because a destroyed one can no longer be asked for its id, and the
			// id is all the list needs to drop the right row.
			public ulong NetworkObjectId { get; }

			public NetworkObject Object { get; }
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

			// The host is already connected when this spawns, so it arrives both through the sweep and
			// through the callback — without this it would be dealt two bodies.
			if (_players.ContainsKey(clientId)) return;

			var spawnPoint = GetRandomSpawnPoint();
			var player = NetworkManager.SpawnManager.InstantiateAndSpawn(_playerPrefab,
				ownerClientId: clientId,
				isPlayerObject: true,
				position: spawnPoint ? spawnPoint.transform.position : Vector3.zero,
				rotation: spawnPoint ? spawnPoint.transform.rotation : Quaternion.identity);

			// Taken now rather than at disconnect: the transport forgets who a connection belonged to
			// the moment it drops.
			var network = GameNetworkManager.Instance;
			var playerId = network ? network.ResolvePlayerId(clientId) : clientId;

			var data = player.GetComponent<PlayerData>();
			if (data) data.ServerSetIdentity(playerId, network ? network.ResolvePlayerName(clientId) : $"Player {clientId}");

			Players.Add(player);
			_players[clientId] = new PlayerEntry(playerId, player);
		}

		private void HandlePlayerDisconnected(ulong clientId)
		{
			if (!IsHost) return;
			if (!_players.Remove(clientId, out var entry)) return;

			RemoveFromPlayers(entry.NetworkObjectId);

			// Where a returning player would reclaim this body instead of losing it.
			if (entry.Object && entry.Object.IsSpawned) entry.Object.Despawn();
		}

		private void HandlePreShutdown()
		{
			if (!IsHost) return;

			// Null-conditional is not enough: a destroyed object answers a plain null check the wrong
			// way round, and despawning something already gone throws.
			foreach (var entry in _players.Values)
			{
				if (entry.Object && entry.Object.IsSpawned) entry.Object.Despawn();
			}

			_players.Clear();
			Players.Clear();
		}

		private void RemoveFromPlayers(ulong networkObjectId)
		{
			for (var i = Players.Count - 1; i >= 0; i--)
			{
				if (Players[i].NetworkObjectId != networkObjectId) continue;

				Players.RemoveAt(i);
				return;
			}
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
