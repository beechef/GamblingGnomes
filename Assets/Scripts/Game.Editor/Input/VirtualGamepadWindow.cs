using Game.Runtime.Controller;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem.LowLevel;

namespace Game.Editor.Input
{
	// Tools/Input/Virtual Gamepad: a pad on screen for Play mode, for testing controller flows without one.
	// A tap holds the button for a few frames so actions read per frame (UI navigation) see it too.
	public class VirtualGamepadWindow : EditorWindow
	{
		private const int TapFrames = 3;

		private static VirtualGamepad _pad;
		private static GamepadKind _kind;
		private static GamepadButton? _tapped;
		private static int _tapFramesLeft;

		[MenuItem("Tools/Input/Virtual Gamepad")]
		private static void Open() => GetWindow<VirtualGamepadWindow>("Virtual Gamepad");

		// The tap and release go through the API too, so an MCP command can drive the same pad in Play mode.
		public static void Tap(GamepadButton button, GamepadKind kind = GamepadKind.Xbox)
		{
			EnsurePad(kind);
			ReleaseTap();

			_tapped = button;
			_tapFramesLeft = TapFrames;
			_pad.Hold(button);

			EditorApplication.update -= TickTap;
			EditorApplication.update += TickTap;
		}

		public static void Disconnect()
		{
			EditorApplication.update -= TickTap;
			_tapped = null;
			_pad?.Dispose();
			_pad = null;
		}

		private static void EnsurePad(GamepadKind kind)
		{
			if (_pad != null && _pad.Device.added && _kind == kind) return;

			Disconnect();
			_kind = kind;
			_pad = VirtualGamepad.Create(kind);
		}

		private static void TickTap()
		{
			if (--_tapFramesLeft > 0) return;

			EditorApplication.update -= TickTap;
			ReleaseTap();
		}

		private static void ReleaseTap()
		{
			if (_tapped.HasValue && _pad != null) _pad.Release(_tapped.Value);
			_tapped = null;
		}

		private void OnEnable() => EditorApplication.playModeStateChanged += HandlePlayModeChanged;

		private void OnDisable() => EditorApplication.playModeStateChanged -= HandlePlayModeChanged;

		private static void HandlePlayModeChanged(PlayModeStateChange change)
		{
			if (change == PlayModeStateChange.ExitingPlayMode) Disconnect();
		}

		private void OnGUI()
		{
			if (!Application.isPlaying)
			{
				EditorGUILayout.HelpBox("Enter Play mode to use the virtual gamepad.", MessageType.Info);
				return;
			}

			var kind = (GamepadKind)EditorGUILayout.EnumPopup("Kind", _kind);
			if (kind != _kind && _pad != null) EnsurePad(kind);
			_kind = kind;

			EditorGUILayout.Space();
			Row(("LB", GamepadButton.LeftShoulder), ("LT", GamepadButton.LeftTrigger), ("RT", GamepadButton.RightTrigger), ("RB", GamepadButton.RightShoulder));
			Row(("View", GamepadButton.Select), ("Menu", GamepadButton.Start));
			EditorGUILayout.Space();
			Row((null, null), ("Up", GamepadButton.DpadUp), (null, null), (null, null), ("Y", GamepadButton.North), (null, null));
			Row(("Left", GamepadButton.DpadLeft), (null, null), ("Right", GamepadButton.DpadRight), ("X", GamepadButton.West), (null, null), ("B", GamepadButton.East));
			Row((null, null), ("Down", GamepadButton.DpadDown), (null, null), (null, null), ("A", GamepadButton.South), (null, null));
			EditorGUILayout.Space();
			Row(("L3", GamepadButton.LeftStick), ("R3", GamepadButton.RightStick));

			EditorGUILayout.Space();
			if (_pad != null && GUILayout.Button("Disconnect")) Disconnect();
		}

		private void Row(params (string label, GamepadButton? button)[] cells)
		{
			using (new EditorGUILayout.HorizontalScope())
			{
				foreach (var (label, button) in cells)
				{
					if (!button.HasValue)
					{
						GUILayout.Space(52f);
						continue;
					}

					if (GUILayout.Button(label, GUILayout.Width(48f), GUILayout.Height(28f))) Tap(button.Value, _kind);
				}
			}
		}
	}
}
