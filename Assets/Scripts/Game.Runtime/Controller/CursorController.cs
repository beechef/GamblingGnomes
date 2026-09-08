using System;
using UnityEngine;

namespace Game.Runtime.Controller
{
	// More than one system wants a say in the cursor at once — a pause screen, a table HUD, a player
	// who pressed the toggle. Each one asks for it to be free and gives that ask back when it is done,
	// and the cursor only locks again once nobody is still asking. A plain bool cannot express that:
	// whoever wrote to it last would win, and closing one panel would yank the cursor out of another.
	//
	// The lock itself is a separate question of whether anything is driving a first person view at all,
	// which is why the menu — with no player alive — leaves the cursor free without anyone asking.
	public static class CursorController
	{
		public static event Action<bool> OnLockChanged;

		public static bool IsLocked => _baseLocked && _unlockRequests <= 0;
		public static int UnlockRequests => _unlockRequests;

		private static bool _baseLocked;
		private static int _unlockRequests;
		private static bool _applied;
		private static bool _appliedLocked;
		private static bool _appliedVisible;

		// Domain Reload is disabled, so statics survive between play sessions.
		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void ResetStatics()
		{
			_baseLocked = false;
			_unlockRequests = 0;
			_applied = false;
			OnLockChanged = null;

			InputSchemeController.OnSchemeChanged -= HandleSchemeChanged;
		}

		// AfterSceneLoad, so this cannot be wiped by InputSchemeController's own reset — the order between
		// two SubsystemRegistration methods is undefined.
		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
		private static void Install()
		{
			InputSchemeController.OnSchemeChanged -= HandleSchemeChanged;
			InputSchemeController.OnSchemeChanged += HandleSchemeChanged;
		}

		private static void HandleSchemeChanged(InputScheme scheme) => Apply();

		// Set by whatever owns the first person view for as long as it lives. Off, nothing below it
		// matters and the cursor stays free.
		public static void SetBaseLocked(bool locked)
		{
			if (_baseLocked == locked) return;

			_baseLocked = locked;
			Apply();
		}

		public static void RequestUnlock()
		{
			_unlockRequests++;
			Apply();
		}

		public static void ReleaseUnlock()
		{
			if (_unlockRequests <= 0)
			{
				// Silence here would leave the cursor locked for whoever is still legitimately asking,
				// with nothing to point at the caller that over-released.
				Debug.LogWarning("[CursorController] ReleaseUnlock without a matching RequestUnlock — the counter is already at zero.");
				return;
			}

			_unlockRequests--;
			Apply();
		}

		private static void Apply()
		{
			var locked = IsLocked;

			// A pointer left floating over a menu being driven by a stick is the clearest sign a game only
			// half supports a pad — the pad moves the selection and the arrow sits there answering nobody.
			var visible = !locked && !InputSchemeController.IsGamepad;

			// Visibility is compared as well as the lock, or picking up a pad while a panel is open would
			// change nothing: the lock state has not moved, and only the arrow was supposed to.
			if (_applied && _appliedLocked == locked && _appliedVisible == visible) return;

			var lockChanged = !_applied || _appliedLocked != locked;

			_applied = true;
			_appliedLocked = locked;
			_appliedVisible = visible;

			Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
			Cursor.visible = visible;

			if (lockChanged) OnLockChanged?.Invoke(locked);
		}
	}
}
