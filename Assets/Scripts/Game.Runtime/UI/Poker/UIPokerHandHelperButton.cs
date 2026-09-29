using Game.Runtime.UI.Button;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.UI.Poker
{
	// The HELPER button: opens the hand ranking board it points at, or closes it again. The board is its own
	// panel and knows nothing of which button opens it.
	public class UIPokerHandHelperButton : MonoBehaviour
	{
		[Required]
		[SerializeField] private UIButton _button;

		[Tooltip("The board this opens. It lives in another panel, so it is wired on the HUD that holds both (UI_Poker).")]
		[Required]
		[SerializeField] private UIPokerHandHelper _helper;

		private void Reset()
		{
			_button = GetComponent<UIButton>();
		}

		private void OnEnable()
		{
			if (_button) _button.OnClick += HandleClick;
		}

		private void OnDisable()
		{
			if (_button) _button.OnClick -= HandleClick;
		}

		private void HandleClick()
		{
			if (_helper) _helper.Toggle();
		}
	}
}
