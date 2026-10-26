using System;
using Steamworks;
using UnityEngine;

namespace Game.Runtime.Steam
{
	public class SteamController : MonoBehaviour
	{
		[Tooltip("The app id used when Steam did not launch the game: the editor and a build started by hand. A build Steam launches uses the id Steam names.")]
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
				SteamClient.Init(ResolveAppId(), false);
				IsInitialized = true;

				// Achievements and stats read as empty until the user's current stats have arrived.
				SteamUserStats.RequestCurrentStats();

				Debug.Log($"[SteamController] Initialized. Local SteamId: {SteamClient.SteamId}");
				OnInitialized?.Invoke();
			}
			catch (Exception e)
			{
				IsInitialized = false;
				// Not an error on its own: Steam may simply not be running, and the lobby falls back to local rooms.
				Debug.LogWarning($"[SteamController] SteamClient.Init failed: {e.Message}");
				OnInitFailed?.Invoke(e.Message);
			}
		}


		// Steam names the app it launched in the environment, and SteamClient.Init overwrites that with whatever it
		// is handed, so it is read first. Never in the editor: a previous Init in the same process left its own id there.
		private uint ResolveAppId()
		{
			if (!Application.isEditor && uint.TryParse(Environment.GetEnvironmentVariable("SteamAppId"), out var launched) && launched != 0)
				return launched;

			return _steamAppId;
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

		// Steam's own on-screen keyboard, for a player with only a pad (Steam Deck, or Big Picture on another
		// handheld). False where Steam has none to show (desktop mode, not initialised): the caller falls back to
		// a physical keyboard. The dismissal handler is a one-shot taken off whichever way it closes.
		public static bool ShowTextInput(string description, string text, int maxCharacters, Action<string> onSubmitted)
		{
			if (!IsInitialized) return false;

			if (!SteamUtils.ShowGamepadTextInput(GamepadTextInputMode.Normal, GamepadTextInputLineMode.SingleLine, description ?? string.Empty, maxCharacters, text ?? string.Empty))
				return false;

			void HandleDismissed(bool submitted)
			{
				SteamUtils.OnGamepadTextInputDismissed -= HandleDismissed;
				if (submitted) onSubmitted?.Invoke(SteamUtils.GetEnteredGamepadText());
			}

			SteamUtils.OnGamepadTextInputDismissed += HandleDismissed;
			return true;
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
