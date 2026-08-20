using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Game.Runtime.Controller
{
	public enum InputScheme
	{
		KeyboardMouse,
		Gamepad,
	}

	// Which kind of device the player is actually holding, so a key badge can name a button they have and
	// a stick can be read as the rate it is rather than the delta a mouse is. Switching is the whole point:
	// nobody picks a scheme in a menu, they pick up a pad, and the game is expected to have noticed.
	//
	// Driven off InputSystem.onEvent rather than onActionChange, because onActionChange only speaks for
	// actions that are enabled and bound — press a pad button on a street where nothing is listening and
	// the scheme would never change, leaving every badge on screen naming a key that is no longer being
	// used. onEvent sees the device whatever the actions are doing.
	public static class InputSchemeController
	{
		public const string KeyboardMouseGroup = "Keyboard&Mouse";
		public const string GamepadGroup = "Gamepad";

		// A stick at rest still streams state and a mouse reports jitter, so an unfiltered event is not
		// evidence anyone touched anything — without this the scheme flips back and forth every frame.
		private const float ActuationThreshold = 0.15f;

		public static event Action<InputScheme> OnSchemeChanged;

		public static InputScheme Current { get; private set; }

		public static bool IsGamepad => Current == InputScheme.Gamepad;

		public static string BindingGroup => IsGamepad ? GamepadGroup : KeyboardMouseGroup;

		// What every display string is masked by, so a badge names the device in hand. A binding belonging
		// to no group matches no mask and comes back empty — every binding must carry its scheme.
		public static InputBinding DisplayMask => InputBinding.MaskByGroup(BindingGroup);

		private static bool _installed;

		// Domain Reload is disabled, so statics survive between play sessions — and a static event is the
		// dangerous one, keeping handlers pointing at objects destroyed two sessions ago.
		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void ResetStatics()
		{
			OnSchemeChanged = null;
			Current = InputScheme.KeyboardMouse;

			if (_installed)
			{
				InputSystem.onEvent -= HandleEvent;
				_installed = false;
			}
		}

		// AfterSceneLoad rather than SubsystemRegistration: the order between two SubsystemRegistration
		// methods is undefined, so anything subscribing to OnSchemeChanged from one of them could be wiped
		// by the reset above running second.
		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
		private static void Install()
		{
			if (_installed) return;

			_installed = true;
			InputSystem.onEvent += HandleEvent;
			Application.quitting += Uninstall;
		}

		private static void Uninstall()
		{
			Application.quitting -= Uninstall;

			if (!_installed) return;

			_installed = false;
			InputSystem.onEvent -= HandleEvent;
		}

		private static void HandleEvent(InputEventPtr eventPtr, InputDevice device)
		{
			if (!eventPtr.IsA<StateEvent>() && !eventPtr.IsA<DeltaStateEvent>()) return;

			var scheme = SchemeFor(device);

			// The early-out before enumerating is what keeps this free on the frames that matter: almost
			// every event comes from the device already in hand.
			if (!scheme.HasValue || scheme.Value == Current) return;

			foreach (var _ in eventPtr.EnumerateChangedControls(device, ActuationThreshold))
			{
				SetScheme(scheme.Value);
				return;
			}
		}

		private static InputScheme? SchemeFor(InputDevice device) => device switch
		{
			Gamepad => InputScheme.Gamepad,
			Keyboard or Mouse => InputScheme.KeyboardMouse,
			_ => null,
		};

		private static void SetScheme(InputScheme scheme)
		{
			if (Current == scheme) return;

			Current = scheme;
			OnSchemeChanged?.Invoke(scheme);
		}
	}
}
