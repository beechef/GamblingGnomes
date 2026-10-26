using Game.Runtime.Controller;
using Game.Runtime.Platform;
using Game.Runtime.Steam;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.Runtime.UI.Input
{
	// A text field a pad can fill. On a pad the field is not opened by landing on it (navigation would end up
	// typing into it); Submit opens an on-screen keyboard instead. Steam's comes first (Steam Deck, Big
	// Picture): it is modal, so the field closes and what was typed is written back. Without it, Windows' touch
	// keyboard types into the open field like a physical one, and is put away when editing ends.
	[RequireComponent(typeof(TMP_InputField))]
	public class UIInputFieldOnScreenKeyboard : MonoBehaviour, ISubmitHandler
	{
		private TMP_InputField _field;
		private bool _touchKeyboardShown;

		private void Awake()
		{
			_field = GetComponent<TMP_InputField>();
		}

		private void OnEnable()
		{
			InputSchemeController.OnSchemeChanged += HandleSchemeChanged;
			_field.onEndEdit.AddListener(HandleEndEdit);

			HandleSchemeChanged(InputSchemeController.Current);
		}

		private void OnDisable()
		{
			_field.onEndEdit.RemoveListener(HandleEndEdit);
			InputSchemeController.OnSchemeChanged -= HandleSchemeChanged;

			HideTouchKeyboard();
		}

		private void HandleSchemeChanged(InputScheme scheme) => _field.shouldActivateOnSelect = scheme != InputScheme.Gamepad;

		// Runs after TMP_InputField's own OnSubmit (it sits below it on the object), which has just opened the field.
		public void OnSubmit(BaseEventData eventData)
		{
			if (!InputSchemeController.IsGamepad || !_field.interactable) return;

			var description = _field.placeholder is TMP_Text placeholder ? placeholder.text : string.Empty;
			var maxCharacters = _field.characterLimit > 0 ? _field.characterLimit : 64;

			if (SteamController.ShowTextInput(description, _field.text, maxCharacters, HandleSubmitted))
			{
				CloseFieldNextFrame();
				return;
			}

			// Opened here as well: TMP only flags the field to open on its next update, so isFocused is not yet true.
			_field.ActivateInputField();
			_touchKeyboardShown = WindowsTouchKeyboard.Show();
		}

		// TMP only flags the field to open on its next update, so closing it now does nothing and it opens
		// under Steam's keyboard; closed two frames later, once it has opened.
		private async void CloseFieldNextFrame()
		{
			try
			{
				await Awaitable.NextFrameAsync(destroyCancellationToken);
				await Awaitable.NextFrameAsync(destroyCancellationToken);
			}
			catch (System.OperationCanceledException)
			{
				return;
			}

			_field.DeactivateInputField();
		}

		private void HandleSubmitted(string text)
		{
			if (!this) return;

			_field.text = text;
		}

		private void HandleEndEdit(string text) => HideTouchKeyboard();

		private void HideTouchKeyboard()
		{
			if (!_touchKeyboardShown) return;

			_touchKeyboardShown = false;
			WindowsTouchKeyboard.Hide();
		}
	}
}
