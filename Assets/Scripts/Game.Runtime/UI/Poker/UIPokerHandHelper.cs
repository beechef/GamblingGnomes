using Game.Runtime.UI.Button;
using UnityEngine;

namespace Game.Runtime.UI.Poker
{
	// The hand ranking board, opened and closed from the HELPER button (or its key). It sits on an object
	// that stays active and switches only the panel, or it would switch itself off with the board and never
	// hear the button again. Escape closes it through UIEscapeStack, so it never pulls the pause menu up.
	public class UIPokerHandHelper : MonoBehaviour
	{
		[Header("References")]
		[SerializeField] private UIButton _button;
		[SerializeField] private GameObject _panel;

		[Tooltip("Optional. Pops the board in on open and fades it out on close; without it the board simply switches.")]
		[SerializeField] private UIPopInVisual _transition;

		// A board fading out is already closed: pressing the button again opens it rather than closing it twice.
		public bool IsOpen => _panel && _panel.activeSelf && !(_transition && _transition.IsHiding);

		private void Awake()
		{
			if (_panel) _panel.SetActive(false);
		}

		private void OnEnable()
		{
			if (_button) _button.OnClick += Toggle;
		}

		private void OnDisable()
		{
			if (_button) _button.OnClick -= Toggle;

			UIEscapeStack.Remove(Close);
			if (_panel) _panel.SetActive(false);
		}

		public void Toggle()
		{
			if (IsOpen) Close();
			else Open();
		}

		public void Open()
		{
			if (!_panel || IsOpen) return;

			// Reopened mid-fade: switching off first cancels the fade, and switching on plays the pop-in again.
			if (_panel.activeSelf) _panel.SetActive(false);

			_panel.SetActive(true);
			UIEscapeStack.Push(Close);
		}

		public void Close()
		{
			UIEscapeStack.Remove(Close);

			if (!IsOpen) return;

			if (_transition) _transition.Hide(HidePanel);
			else HidePanel();
		}

		private void HidePanel()
		{
			if (_panel && _panel.activeSelf) _panel.SetActive(false);
		}
	}
}
