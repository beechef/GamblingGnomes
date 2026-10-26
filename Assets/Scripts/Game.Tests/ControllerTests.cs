using System.Collections;
using System.Collections.Generic;
using Game.Runtime.Controller;
using Game.Runtime.UI.Input;
using Game.Runtime.UI.Selection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Game.Tests
{
	// Play mode tests driving the game's controller support with a VirtualGamepad: the scheme switch, the
	// glyphs it draws, and focus moving between UIFocusRoots the way a pad player moves through panels.
	public class ControllerTests
	{
		private const string GlyphDatabasePath = "Assets/Configs/InputGlyphDatabase.asset";

		private VirtualGamepad _pad;
		private Keyboard _keyboard;
		private GameObject _ui;

		private readonly List<InputDevice> _silenced = new();
		private InputSettings.BackgroundBehavior _backgroundBehavior;
		private InputSettings.EditorInputBehaviorInPlayMode _editorBehavior;

		// The editor's own mouse and keyboard would flip the scheme back mid-test, and an unfocused Game view
		// drops pad input, so the real devices are silenced and input runs regardless of focus.
		[SetUp]
		public void SetUp()
		{
			foreach (var device in InputSystem.devices)
			{
				if (!device.native || !device.enabled) continue;

				InputSystem.DisableDevice(device);
				_silenced.Add(device);
			}

			// Changed on the live settings and put back, never swapped for a copy: the editor serializes the
			// settings reference across a domain reload, and a copy that is not an asset comes back null.
			var settings = InputSystem.settings;
			_backgroundBehavior = settings.backgroundBehavior;
			_editorBehavior = settings.editorInputBehaviorInPlayMode;
			settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
			settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
		}

		[TearDown]
		public void TearDown()
		{
			_pad?.Dispose();
			_pad = null;

			if (_keyboard != null && _keyboard.added) InputSystem.RemoveDevice(_keyboard);
			_keyboard = null;

			if (_ui) Object.Destroy(_ui);

			var settings = InputSystem.settings;
			settings.backgroundBehavior = _backgroundBehavior;
			settings.editorInputBehaviorInPlayMode = _editorBehavior;

			foreach (var device in _silenced)
			{
				if (device.added) InputSystem.EnableDevice(device);
			}

			_silenced.Clear();
		}

		[UnityTest]
		public IEnumerator PadPress_SwitchesSchemeToGamepad()
		{
			yield return UseKeyboard();

			_pad = VirtualGamepad.Create(GamepadKind.Xbox);
			yield return Tap(GamepadButton.South);

			Assert.That(InputSchemeController.Current, Is.EqualTo(InputScheme.Gamepad));
			Assert.That(InputSchemeController.CurrentGamepadKind, Is.EqualTo(GamepadKind.Xbox));
		}

		[UnityTest]
		public IEnumerator DualShockPress_IsPlayStationKind()
		{
			_pad = VirtualGamepad.Create(GamepadKind.PlayStation);
			yield return Tap(GamepadButton.South);

			Assert.That(InputSchemeController.IsGamepad, Is.True);
			Assert.That(InputSchemeController.CurrentGamepadKind, Is.EqualTo(GamepadKind.PlayStation));
		}

		[UnityTest]
		public IEnumerator KeyPress_SwitchesBackToKeyboard()
		{
			_pad = VirtualGamepad.Create(GamepadKind.Xbox);
			yield return Tap(GamepadButton.South);
			Assume.That(InputSchemeController.IsGamepad, Is.True);

			yield return UseKeyboard();

			Assert.That(InputSchemeController.Current, Is.EqualTo(InputScheme.KeyboardMouse));
		}

		[UnityTest]
		public IEnumerator Glyphs_FollowTheDeviceInHand()
		{
			var database = LoadGlyphDatabase();
			var submit = database.FindAction("UI/Submit");
			var cancel = database.FindAction("UI/Cancel");

			_pad = VirtualGamepad.Create(GamepadKind.PlayStation);
			yield return Tap(GamepadButton.South);

			Assert.That(database.TryResolve(submit, out var sprite, out _), Is.True);
			Assert.That(sprite, Is.EqualTo("buttonSouth"));
			Assert.That(database.Current.name, Does.Contain("PlayStation"));

			// UI/Cancel is a usage binding (*/{Cancel}); it resolves through the pad's own control.
			Assert.That(database.TryResolve(cancel, out sprite, out _), Is.True);
			Assert.That(sprite, Is.EqualTo("buttonEast"));

			yield return UseKeyboard();

			Assert.That(database.TryResolve(submit, out sprite, out _), Is.True);
			Assert.That(sprite, Is.EqualTo("enter"));
			Assert.That(database.Current.name, Does.Contain("KeyboardMouse"));
		}

		[UnityTest]
		public IEnumerator GlyphText_ExpandsActionTags()
		{
			_pad = VirtualGamepad.Create(GamepadKind.Xbox);
			yield return Tap(GamepadButton.South);

			_ui = new GameObject("GlyphText", typeof(RectTransform));
			_ui.SetActive(false);
			_ui.AddComponent<TextMeshProUGUI>();
			var glyphs = _ui.AddComponent<UIInputGlyphText>();
			SetDatabase(glyphs, LoadGlyphDatabase());
			_ui.SetActive(true);

			var text = glyphs.PreprocessText("<b>Press</b> <UI/Submit>, <NotAnAction>");

			Assert.That(text, Is.EqualTo("<b>Press</b> <sprite name=\"buttonSouth\">, <NotAnAction>"));
		}

		[UnityTest]
		public IEnumerator FocusRoot_TakesFocusAndHandsItBack()
		{
			_pad = VirtualGamepad.Create(GamepadKind.Xbox);
			yield return Tap(GamepadButton.South);

			BuildUi(out var hudLeft, out var hudRight, out var panel, out var panelButton);
			yield return null;
			yield return null;

			Assert.That(Selected, Is.EqualTo(hudLeft.gameObject), "the HUD root focuses its first button");

			yield return Hold(GamepadButton.DpadRight);
			Assert.That(Selected, Is.EqualTo(hudRight.gameObject), "d-pad right navigates inside the HUD");

			panel.SetActive(true);
			yield return null;
			yield return null;
			Assert.That(Selected, Is.EqualTo(panelButton.gameObject), "a shown panel takes focus");

			yield return Hold(GamepadButton.DpadLeft);
			Assert.That(Selected, Is.EqualTo(panelButton.gameObject), "a confining panel keeps the pad inside");

			panel.SetActive(false);
			yield return null;
			Assert.That(Selected, Is.EqualTo(hudRight.gameObject), "hiding the panel hands focus back to the opener");

			yield return Hold(GamepadButton.DpadLeft);
			Assert.That(Selected, Is.EqualTo(hudLeft.gameObject), "navigation outside the panel works again");
		}

		private static GameObject Selected => EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;

		private void BuildUi(out UnityEngine.UI.Button hudLeft, out UnityEngine.UI.Button hudRight, out GameObject panel, out UnityEngine.UI.Button panelButton)
		{
			_ui = new GameObject("ControllerTestUi");

			var events = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
			events.transform.SetParent(_ui.transform);
			events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
			EventSystem.current = events.GetComponent<EventSystem>();

			var canvas = new GameObject("Canvas", typeof(Canvas), typeof(GraphicRaycaster));
			canvas.transform.SetParent(_ui.transform);
			canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

			var hud = new GameObject("Hud", typeof(RectTransform));
			hud.transform.SetParent(canvas.transform, false);
			hud.SetActive(false);
			hud.AddComponent<UIFocusRoot>();
			hudLeft = MakeButton("HudLeft", hud.transform, new Vector2(-200f, 0f));
			hudRight = MakeButton("HudRight", hud.transform, new Vector2(200f, 0f));
			hud.SetActive(true);

			panel = new GameObject("Panel", typeof(RectTransform));
			panel.transform.SetParent(canvas.transform, false);
			panel.SetActive(false);
			SetConfine(panel.AddComponent<UIFocusRoot>());
			panelButton = MakeButton("PanelButton", panel.transform, new Vector2(400f, 200f));
		}

		private static UnityEngine.UI.Button MakeButton(string name, Transform parent, Vector2 position)
		{
			var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(UnityEngine.UI.Button));
			go.transform.SetParent(parent, false);
			var rect = (RectTransform)go.transform;
			rect.sizeDelta = new Vector2(120f, 50f);
			rect.anchoredPosition = position;
			return go.GetComponent<UnityEngine.UI.Button>();
		}

		private static void SetConfine(UIFocusRoot root)
		{
#if UNITY_EDITOR
			var so = new UnityEditor.SerializedObject(root);
			so.FindProperty("_confineNavigation").boolValue = true;
			so.ApplyModifiedPropertiesWithoutUndo();
#endif
		}

		private static void SetDatabase(UIInputGlyphText text, InputGlyphDatabase database)
		{
#if UNITY_EDITOR
			var so = new UnityEditor.SerializedObject(text);
			so.FindProperty("_database").objectReferenceValue = database;
			so.ApplyModifiedPropertiesWithoutUndo();
#endif
		}

		private static InputGlyphDatabase LoadGlyphDatabase()
		{
#if UNITY_EDITOR
			var database = UnityEditor.AssetDatabase.LoadAssetAtPath<InputGlyphDatabase>(GlyphDatabasePath);
			Assume.That(database, Is.Not.Null, GlyphDatabasePath);
			return database;
#else
			Assert.Ignore("Needs the editor's AssetDatabase.");
			return null;
#endif
		}

		private IEnumerator UseKeyboard()
		{
			if (_keyboard == null) _keyboard = InputSystem.AddDevice<Keyboard>("VirtualKeyboard");

			InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Key.Space));
			yield return null;
			InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
			yield return null;
		}

		private IEnumerator Tap(GamepadButton button)
		{
			_pad.Hold(button);
			yield return null;
			_pad.Release(button);
			yield return null;
		}

		// Held across frames, since the input module reads navigation once per frame.
		private IEnumerator Hold(GamepadButton button)
		{
			_pad.Hold(button);
			yield return null;
			yield return null;
			_pad.Release(button);
			yield return null;
			yield return null;
		}
	}
}
