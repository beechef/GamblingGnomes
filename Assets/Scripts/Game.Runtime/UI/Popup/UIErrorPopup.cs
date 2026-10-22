using Game.Runtime.UI.Button;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

namespace Game.Runtime.UI.Popup
{
	// A message the player has to dismiss. The root stays active and toggles _content, so whoever feeds it can
	// show it whatever screen is up; it pops in over everything as the last sibling.
	public class UIErrorPopup : MonoBehaviour
	{
		[Required]
		[SerializeField] private GameObject _content;

		[Required]
		[SerializeField] private TMP_Text _message;

		[Tooltip("The corner cross; Escape presses it.")]
		[Required]
		[SerializeField] private UIButton _closeButton;

		[SerializeField] private UIButton _confirmButton;

		private UIPopInVisual _transition;

		public bool IsOpen { get; private set; }

		private void Awake()
		{
			_transition = _content.GetComponentInChildren<UIPopInVisual>(true);
			_content.SetActive(false);
		}

		private void OnEnable()
		{
			_closeButton.OnClick += Hide;
			if (_confirmButton) _confirmButton.OnClick += Hide;
		}

		private void OnDisable()
		{
			if (_confirmButton) _confirmButton.OnClick -= Hide;
			_closeButton.OnClick -= Hide;
		}

		private void OnDestroy() => UIScreenCover.Set(this, false);

		public void Show(string message)
		{
			IsOpen = true;
			_message.text = message;
			transform.SetAsLastSibling();

			// Brought back mid-fade: switching off first cancels the fade and lets the pop-in play again.
			if (_content.activeSelf && _transition && _transition.IsHiding) _content.SetActive(false);
			_content.SetActive(true);

			// The menu's 3D models stand in front of the canvas and would draw over the paper.
			UIScreenCover.Set(this, true);
		}

		public void Hide()
		{
			if (!IsOpen) return;
			IsOpen = false;

			if (_transition) _transition.Hide(SwitchOffIfStillHidden);
			else SwitchOffIfStillHidden();
		}

		private void SwitchOffIfStillHidden()
		{
			if (IsOpen) return;

			_content.SetActive(false);
			UIScreenCover.Set(this, false);
		}
	}
}
