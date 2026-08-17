using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Runtime.UI.Wheel
{
	// Two bound actions, one step each. Serialized as InputActionReferences rather than looked up by
	// name, so a rebind follows automatically and a typo cannot survive to runtime.
	public class UIWheelActionInput : MonoBehaviour, IUIWheelInput
	{
		public event Action<UIWheelDirection> OnStepped;

		[Header("Input")]
		[SerializeField] private InputActionReference _backwardAction;
		[SerializeField] private InputActionReference _forwardAction;

		private void OnEnable()
		{
			Bind(_backwardAction, HandleBackward);
			Bind(_forwardAction, HandleForward);
		}

		private void OnDisable()
		{
			Unbind(_backwardAction, HandleBackward);
			Unbind(_forwardAction, HandleForward);
		}

		private static void Bind(InputActionReference reference, Action<InputAction.CallbackContext> handler)
		{
			if (!reference) return;

			reference.action.performed += handler;
			reference.action.Enable();
		}

		private static void Unbind(InputActionReference reference, Action<InputAction.CallbackContext> handler)
		{
			if (!reference) return;

			reference.action.performed -= handler;
		}

		private void HandleBackward(InputAction.CallbackContext context) => OnStepped?.Invoke(UIWheelDirection.Backward);
		private void HandleForward(InputAction.CallbackContext context) => OnStepped?.Invoke(UIWheelDirection.Forward);
	}
}
