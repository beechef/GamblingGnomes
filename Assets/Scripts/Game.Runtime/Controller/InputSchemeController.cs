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

	public enum GamepadKind
	{
		Xbox,
		PlayStation,
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

		// Raised on a scheme change and on a pad of another kind taking over, for anything drawn per device.
		public static event Action OnDeviceChanged;

		public static InputScheme Current { get; private set; }

		public static GamepadKind CurrentGamepadKind { get; private set; }

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
			OnDeviceChanged = null;
			Current = InputScheme.KeyboardMouse;
			CurrentGamepadKind = GamepadKind.Xbox;

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
			if (!scheme.HasValue) return;

			var kind = scheme.Value == InputScheme.Gamepad ? KindFor(device) : CurrentGamepadKind;

			// The early-out before enumerating is what keeps this free on the frames that matter: almost
			// every event comes from the device already in hand.
			if (scheme.Value == Current && kind == CurrentGamepadKind) return;

			foreach (var _ in eventPtr.EnumerateChangedControls(device, ActuationThreshold))
			{
				SetDevice(scheme.Value, kind);
				return;
			}
		}

		// A device the game invented is not hardware in anybody's hands. VirtualMouseInput creates a real
		// Mouse for the pad's cursor, so without this the right stick reports a mouse and flips the scheme
		// straight back to keyboard — which then switches off the very cursor that was reporting, every
		// time the player moves it. Only an invented pointer is refused: a pad or keyboard added in code is a test
		// or the editor's virtual gamepad standing in for one, and must switch the scheme like the real thing.
		private static InputScheme? SchemeFor(InputDevice device)
		{
			if (device == null || (!device.native && device is Pointer)) return null;

			return device switch
			{
				Gamepad => InputScheme.Gamepad,
				Keyboard or Mouse => InputScheme.KeyboardMouse,
				_ => null,
			};
		}

		// Anything that is not a PlayStation pad is drawn as Xbox, the layout every other pad copies.
		private static GamepadKind KindFor(InputDevice device) =>
			device is UnityEngine.InputSystem.DualShock.DualShockGamepad ? GamepadKind.PlayStation : GamepadKind.Xbox;

		private static void SetDevice(InputScheme scheme, GamepadKind kind)
		{
			var schemeChanged = Current != scheme;

			Current = scheme;
			CurrentGamepadKind = kind;

			if (schemeChanged) OnSchemeChanged?.Invoke(scheme);
			OnDeviceChanged?.Invoke();
		}
	}
}
