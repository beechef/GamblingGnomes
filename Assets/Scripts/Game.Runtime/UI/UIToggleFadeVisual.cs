using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Runtime.UI
{
	// Fades a toggle's mark in and out on its own clock. uGUI's Toggle fades its graphic over a fixed 0.1 s, so
	// the toggle is given no graphic and this draws the mark instead.
	public class UIToggleFadeVisual : MonoBehaviour
	{
		[Required]
		[SerializeField] private Toggle _toggle;

		[Required]
		[SerializeField] private Graphic _mark;

		[MinValue(0f)]
		[SerializeField] private float _duration = 0.05f;

		private void OnEnable()
		{
			_toggle.onValueChanged.AddListener(HandleValueChanged);
			Apply(_toggle.isOn, true);
		}

		private void OnDisable() => _toggle.onValueChanged.RemoveListener(HandleValueChanged);

		private void HandleValueChanged(bool isOn) => Apply(isOn, false);

		// Unscaled, so the box still answers while the pause menu has the game stopped.
		private void Apply(bool isOn, bool instant) => _mark.CrossFadeAlpha(isOn ? 1f : 0f, instant ? 0f : _duration, true);
	}
}
