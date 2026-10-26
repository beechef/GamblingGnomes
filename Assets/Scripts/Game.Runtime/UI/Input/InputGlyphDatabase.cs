using System;
using Game.Runtime.Controller;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Runtime.UI.Input
{
	// One sprite asset per device set, each sprite named after the control it draws ("buttonSouth",
	// "dpad_up", "leftButton"), so a binding's path is its sprite's name and no table can drift from it.
	[CreateAssetMenu(fileName = "InputGlyphDatabase", menuName = "Game/UI/Input Glyph Database")]
	public class InputGlyphDatabase : ScriptableObject
	{
		[Required]
		[SerializeField] private InputActionAsset _actions;

		[Header("Sprite Sets")]
		[Required]
		[SerializeField] private TMP_SpriteAsset _keyboardMouse;

		[Required]
		[SerializeField] private TMP_SpriteAsset _xbox;

		[Required]
		[SerializeField] private TMP_SpriteAsset _playStation;

		public TMP_SpriteAsset Current
		{
			get
			{
				if (!InputSchemeController.IsGamepad) return _keyboardMouse;

				return InputSchemeController.CurrentGamepadKind == GamepadKind.PlayStation && _playStation
					? _playStation
					: _xbox;
			}
		}

		public InputAction FindAction(string actionName) => _actions ? _actions.FindAction(actionName) : null;

		// The sprite for the action's binding in the scheme in hand; with none drawn for it, the binding's
		// display string, so an undrawn key still reads as text rather than vanishing.
		public bool TryResolve(InputAction action, out string spriteName, out string fallbackText)
		{
			spriteName = null;
			fallbackText = string.Empty;

			if (action == null) return false;

			var index = action.GetBindingIndex(InputSchemeController.DisplayMask);
			if (index < 0) return false;

			fallbackText = action.GetBindingDisplayString(index);

			var name = ControlName(action.bindings[index].effectivePath) ?? ControlNameInHand(action);
			var set = Current;
			if (!set || string.IsNullOrEmpty(name) || set.GetSpriteIndexFromName(name) < 0) return false;

			spriteName = name;
			return true;
		}

		public Sprite FindSprite(string spriteName)
		{
			var set = Current;
			var index = set ? set.GetSpriteIndexFromName(spriteName) : -1;
			if (index < 0) return null;

			return set.spriteCharacterTable[index].glyph is TMP_SpriteGlyph glyph ? glyph.sprite : null;
		}

		// "<Gamepad>/dpad/up" → "dpad_up". A usage path ("*/{Cancel}") names no control, so it returns null.
		private static string ControlName(string path)
		{
			if (string.IsNullOrEmpty(path)) return null;

			var start = path.IndexOf(">/", StringComparison.Ordinal);
			if (start < 0 || path.IndexOf('{') >= 0) return null;

			return path.Substring(start + 2).Replace('/', '_');
		}

		// What a usage binding resolved to on the device in hand ("/DualShock4GamepadHID/buttonEast" → "buttonEast").
		private static string ControlNameInHand(InputAction action)
		{
			foreach (var control in action.controls)
			{
				var device = control.device;
				var inHand = InputSchemeController.IsGamepad ? device is Gamepad : device is Keyboard or Mouse;
				if (!inHand) continue;

				var path = control.path;
				var start = path.IndexOf('/', 1);
				return start < 0 ? null : path.Substring(start + 1).Replace('/', '_');
			}

			return null;
		}
	}
}
