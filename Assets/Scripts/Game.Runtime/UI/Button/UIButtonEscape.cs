using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.UI.Button
{
	// Escape presses this button while it is shown — a panel's close cross, or its Back when it has one. Goes through UIEscapeStack rather
	// than binding a key on the button, because Escape is shared: the newest button shown gets the press, and
	// the pause menu only opens when no button is waiting for it.
	[RequireComponent(typeof(UIButton))]
	public class UIButtonEscape : MonoBehaviour
	{
		[Required]
		[SerializeField] private UIButton _button;

		[Tooltip("Pressed instead while it is shown and answers: a panel's Back, so Escape steps back one panel and only the cross closes them all.")]
		[SerializeField] private UIButton _backButton;

		private void Reset() => _button = GetComponent<UIButton>();

		private void OnEnable() => UIEscapeStack.Push(Press);

		private void OnDisable() => UIEscapeStack.Remove(Press);

		private void Press()
		{
			var target = _backButton && _backButton.isActiveAndEnabled && _backButton.IsInteractable ? _backButton : _button;
			target.Submit();

			// Taken off the stack to be pressed; a button still up afterwards keeps answering the next Escape.
			if (isActiveAndEnabled) UIEscapeStack.Push(Press);
		}
	}
}
