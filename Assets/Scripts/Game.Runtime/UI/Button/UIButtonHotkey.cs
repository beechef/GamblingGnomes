using Game.Runtime.Controller;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Runtime.UI.Button
{
	// A key wired into a button. It talks to UIButton and nothing else — the same component a click ends
	// up at — so the key runs the same guard, raises the same event and drives the same states, and the
	// uGUI Button underneath stays an implementation detail nobody reaches past this for.
	[RequireComponent(typeof(UIButton))]
	public class UIButtonHotkey : MonoBehaviour
	{
		[Header("Input")]
		[SerializeField] private InputActionReference _action;

		[Header("References")]
		[Tooltip("Optional. Shows the key on the button face, read off the binding itself so it can never lie.")]
		[SerializeField] private TMP_Text _keyLabel;

		private UIButton _button;

		private void Awake()
		{
			_button = GetComponent<UIButton>();
		}

		private void OnEnable()
		{
			if (_action)
			{
				// started/canceled bracket the key being held; performed is the press itself. Taken from the
				// action rather than timed here, so holding the key holds the button, exactly as a mouse does.
				_action.action.started += HandleStarted;
				_action.action.performed += HandlePerformed;
				_action.action.canceled += HandleCanceled;
				_action.action.Enable();
			}

			InputSchemeController.OnSchemeChanged += HandleSchemeChanged;

			RefreshLabel();
		}

		private void OnDisable()
		{
			if (_action)
			{
				_action.action.started -= HandleStarted;
				_action.action.performed -= HandlePerformed;
				_action.action.canceled -= HandleCanceled;
				_action.action.Disable();
			}

			InputSchemeController.OnSchemeChanged -= HandleSchemeChanged;

			Release();
		}

		private void HandleSchemeChanged(InputScheme scheme) => RefreshLabel();

		private void HandleStarted(InputAction.CallbackContext context)
		{
			if (_button && _button.IsInteractable) _button.IsPressedByKey = true;
		}

		private void HandlePerformed(InputAction.CallbackContext context)
		{
			if (_button) _button.Submit();
		}

		private void HandleCanceled(InputAction.CallbackContext context) => Release();

		// Called from every way out, so a key still down when the button is switched off does not leave it
		// stuck looking pressed the next time it appears.
		private void Release()
		{
			if (_button) _button.IsPressedByKey = false;
		}

		private void RefreshLabel()
		{
			if (!_keyLabel) return;

			_keyLabel.text = _action
				? _action.action.GetBindingDisplayString(InputSchemeController.DisplayMask)
				: string.Empty;
		}
	}
}
