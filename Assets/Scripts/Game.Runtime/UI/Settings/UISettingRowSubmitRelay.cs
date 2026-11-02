using Game.Runtime.UI.Button;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.Runtime.UI.Settings
{
	// A row whose control is a toggle or a text field: pressing the row (Submit on a pad, a click beside the
	// control) is handed to the control as its own Submit, so it flips or opens as if it had been the one pressed.
	public class UISettingRowSubmitRelay : MonoBehaviour
	{
		[SerializeField] private UIButton _row;
		[SerializeField] private GameObject _control;

		private void Awake() => _row.OnClick += HandleRowClicked;

		private void OnDestroy()
		{
			if (_row) _row.OnClick -= HandleRowClicked;
		}

		private void HandleRowClicked()
		{
			if (!_control || !EventSystem.current) return;

			ExecuteEvents.Execute(_control, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
		}
	}
}
