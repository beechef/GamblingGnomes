using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.UI.Button
{
	// Escape presses this button while it is shown — a panel's close cross. Goes through UIEscapeStack rather
	// than binding a key (UIButtonHotkey), because Escape is shared: the newest button shown gets the press, and
	// the pause menu only opens when no button is waiting for it.
	[RequireComponent(typeof(UIButton))]
	public class UIButtonEscape : MonoBehaviour
	{
		[Required]
		[SerializeField] private UIButton _button;

		private void Reset() => _button = GetComponent<UIButton>();

		private void OnEnable() => UIEscapeStack.Push(Press);

		private void OnDisable() => UIEscapeStack.Remove(Press);

		private void Press()
		{
			_button.Submit();

			// Taken off the stack to be pressed; a button still up afterwards keeps answering the next Escape.
			if (isActiveAndEnabled) UIEscapeStack.Push(Press);
		}
	}
}
