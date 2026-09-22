using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Runtime.Controller
{
	// Several modes share one gameplay scene, so the mode is not placed in it: the server spawns the prefab
	// the host picked in the lobby into this scene, and every client receives it through the spawn.
	public class GameModeController : NetworkBehaviour
	{
		private NetworkObject _mode;

		// After the whole scene has spawned rather than from OnNetworkSpawn: the mode lays the table from the
		// seats registered when it spawns, and a chair later in the scene would not be counted yet.
		protected override void OnInSceneObjectsSpawned()
		{
			if (!IsServer || _mode) return;

			SpawnSelectedMode();
		}

		// No despawn here: the session shuts down before the scene unloads, and Netcode takes every spawned
		// object down with it.
		public override void OnNetworkDespawn()
		{
			_mode = null;
		}

		private void SpawnSelectedMode()
		{
			var network = GameNetworkManager.Instance;
			if (!network)
			{
				Debug.LogError("[GameModeController] No GameNetworkManager, so there is no selected mode to spawn.", this);
				return;
			}

			if (!network.TryGetSelectedGameMode(out var entry))
			{
				Debug.LogError($"[GameModeController] No GameModeDatabase entry for mode {network.LobbySettings.SelectedGameMode}.", this);
				return;
			}

			if (!entry.ModePrefab || !entry.ModePrefab.TryGetComponent<NetworkObject>(out _))
			{
				Debug.LogError($"[GameModeController] Mode {entry.GameModeType} has no ModePrefab with a NetworkObject.", this);
				return;
			}

			var instance = Instantiate(entry.ModePrefab);
			SceneManager.MoveGameObjectToScene(instance, gameObject.scene);

			_mode = instance.GetComponent<NetworkObject>();
			_mode.Spawn(destroyWithScene: true);
		}
	}
}
