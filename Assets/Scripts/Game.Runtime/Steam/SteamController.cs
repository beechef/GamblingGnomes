using System;
using Steamworks;
using UnityEngine;

namespace Game.Runtime.Steam
{
	public class SteamController : MonoBehaviour
	{
		[SerializeField] private uint _steamAppId = 480;

		public static SteamController Instance { get; private set; }
		public static bool IsInitialized { get; private set; }
		public static SteamId LocalSteamId => IsInitialized ? SteamClient.SteamId : default;

		public static event Action OnInitialized;
		public static event Action<string> OnInitFailed;
		public static event Action OnShutdown;

		private bool _relayNetworkReady;

		// Domain Reload is disabled, so statics survive between play sessions.
		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void ResetStatics()
		{
			Instance = null;
			IsInitialized = false;
			OnInitialized = null;
			OnInitFailed = null;
			OnShutdown = null;
		}

		private void Awake()
		{
			if (Instance != null && Instance != this)
			{
				Destroy(gameObject);
				return;
			}
			Instance = this;

			InitializeSteam();
		}

		private void InitializeSteam()
		{
			if (IsInitialized)
			{
				if (Application.isEditor)
					Debug.Log("[SteamController] Already initialized, skipping.");
				return;
			}

			try
			{
				SteamClient.Init(_steamAppId, false);
				IsInitialized = true;

				Debug.Log($"[SteamController] Initialized. Local SteamId: {SteamClient.SteamId}");
				OnInitialized?.Invoke();
			}
			catch (Exception e)
			{
				IsInitialized = false;
				Debug.LogError($"[SteamController] SteamClient.Init failed: {e.Message}");
				OnInitFailed?.Invoke(e.Message);
			}
		}


		private void Update()
		{
			if (!IsInitialized)
				return;

			SteamClient.RunCallbacks();

			if (!_relayNetworkReady)
			{
				_relayNetworkReady = true;
				SteamNetworkingUtils.InitRelayNetworkAccess();
				Debug.Log("[SteamController] Relay network access initialized.");
			}
		}

		private void ShutdownSteam()
		{
			if (!IsInitialized)
				return;

			try
			{
				SteamClient.Shutdown();
			}
			catch (Exception e)
			{
				Debug.LogError($"[SteamController] Exception during shutdown: {e.Message}");
			}
			finally
			{
				IsInitialized = false;
				_relayNetworkReady = false;
				OnShutdown?.Invoke();
				Debug.Log("[SteamController] Shut down.");
			}
		}

		private void OnApplicationQuit()
		{
			ShutdownSteam();
		}

		private void OnDestroy()
		{
			if (Instance == this)
				Instance = null;
		}
	}
}
