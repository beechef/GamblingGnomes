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

		// Keyed by connection only. The identity that outlives one lives on the player object itself,
		// which now survives the disconnect — so matching a returning player is a question for their
		// PlayerData, not for a copy kept here.
		private readonly Dictionary<ulong, PlayerEntry> _players = new();

		// Which colours are spoken for. Held as a set of claims rather than counted off the player list,
		// because the whole point is that a player leaving mid-game leaves a *hole*: the next arrival takes
		// the lowest free number, not the next one up, so a table that has churned all evening still has
		// everybody in the first few colours instead of drifting off the end of the palette.
		private readonly HashSet<int> _claimedColorIndices = new();

		private readonly struct PlayerEntry
		{
			public PlayerEntry(NetworkObject player, int colorIndex)
			{
				NetworkObjectId = player.NetworkObjectId;
				Object = player;
				ColorIndex = colorIndex;
			}

			// Held beside the object because a destroyed one can no longer be asked for its id, and the
			// id is all the list needs to drop the right row.
			public ulong NetworkObjectId { get; }

			public NetworkObject Object { get; }

			// Kept here for the same reason: the claim has to be given back after the body is gone.
			public int ColorIndex { get; }
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

			var colorIndex = ClaimColorIndex();

			// Written straight after the spawn rather than from the player's own OnNetworkSpawn: which
			// colour is free is a question about the whole table, and only this side of it knows.
			var data = player.GetComponent<PlayerData>();
			if (data) data.ServerSetColorIndex(colorIndex);

			Players.Add(player);
			_players[clientId] = new PlayerEntry(player, colorIndex);
		}

		// Lowest free, so a seat vacated mid-game is the one the next arrival walks into.
		private int ClaimColorIndex()
		{
			for (var index = 0; ; index++)
			{
				if (_claimedColorIndices.Add(index)) return index;
			}
		}

		private void HandlePlayerDisconnected(ulong clientId)
		{
			if (!IsHost) return;
			if (!_players.Remove(clientId, out var entry)) return;

			_claimedColorIndices.Remove(entry.ColorIndex);

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
			_claimedColorIndices.Clear();
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
