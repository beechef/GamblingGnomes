using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Runtime.UI.Settings
{
	// Moves a slider by a fixed share of its range when its row is pushed left or right.
	public class UISliderStepper : MonoBehaviour
	{
		[Required] [SerializeField] private UISettingRow _row;
		[Required] [SerializeField] private Slider _slider;

		[PropertyRange(0.01f, 1f)]
		[SerializeField] private float _step = 0.1f;

		private void OnEnable() => _row.OnStepped += Step;

		private void OnDisable() => _row.OnStepped -= Step;

		private void Step(int direction)
		{
			if (_slider.IsInteractable()) _slider.normalizedValue += direction * _step;
		}
	}
}
