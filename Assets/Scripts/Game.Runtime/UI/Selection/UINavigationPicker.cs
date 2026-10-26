using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Game.Runtime.UI.Selection
{
	// Steps through things that are not uGUI selectables (bodies at the table, cards in a hand) with the pad's
	// navigation, the way a list steps through buttons. While active, its empty confining UIFocusRoot is the
	// newest root, so no button navigates; input is read only while that root is the newest, so the pause menu
	// stacks over a pick and the pick carries on when it closes. What is stepped through, and what Back does,
	// stay with the owner.
	public sealed class UINavigationPicker
	{
		private readonly UIFocusRoot _root;
		private readonly InputActionReference _navigateAction;
		private readonly InputActionReference _submitAction;
		private readonly float _stepThreshold;

		private bool _stepHeld;
		private int _beganFrame;

		// Left or right, as -1 or 1.
		public event Action<int> OnStep;
		public event Action OnSubmit;

		public bool IsActive => _root && _root.gameObject.activeSelf;

		public UINavigationPicker(UIFocusRoot root, InputActionReference navigateAction, InputActionReference submitAction, float stepThreshold = 0.5f)
		{
			_root = root;
			_navigateAction = navigateAction;
			_submitAction = submitAction;
			_stepThreshold = stepThreshold;

			if (_root) _root.gameObject.SetActive(false);
		}

		public void Begin()
		{
			if (IsActive || !_root) return;

			// Switched on before focus is cleared, so the root remembers what had focus to hand it back to.
			_root.gameObject.SetActive(true);

			var events = EventSystem.current;
			if (events) events.SetSelectedGameObject(null);

			_beganFrame = Time.frameCount;
			_stepHeld = true;

			if (_navigateAction)
			{
				_navigateAction.action.performed += HandleNavigate;
				_navigateAction.action.canceled += HandleNavigate;
			}

			if (_submitAction) _submitAction.action.performed += HandleSubmit;
		}

		public void End()
		{
			if (!IsActive) return;

			if (_submitAction) _submitAction.action.performed -= HandleSubmit;

			if (_navigateAction)
			{
				_navigateAction.action.canceled -= HandleNavigate;
				_navigateAction.action.performed -= HandleNavigate;
			}

			// Last, so the root hands focus back.
			_root.gameObject.SetActive(false);
		}

		private bool HasInput => IsActive && UIFocusRoot.Top == _root;

		// One step per lean: the stick has to come back under the threshold before the next.
		private void HandleNavigate(InputAction.CallbackContext context)
		{
			var x = context.ReadValue<Vector2>().x;

			if (Mathf.Abs(x) < _stepThreshold)
			{
				_stepHeld = false;
				return;
			}

			if (_stepHeld || !HasInput) return;

			_stepHeld = true;
			OnStep?.Invoke(x > 0f ? 1 : -1);
		}

		// The press that opened the pick may be the same Submit; it must not also commit.
		private void HandleSubmit(InputAction.CallbackContext context)
		{
			if (!HasInput || Time.frameCount == _beganFrame) return;

			OnSubmit?.Invoke();
		}
	}
}
