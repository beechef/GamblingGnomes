using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.UI.Button
{
	// One half of the widget split: the button decides, this draws. Subscribing and snapping to the
	// state already standing are the base's job — a subclass only says what a state looks like, and
	// cannot forget to unsubscribe or leave itself showing the wrong thing on the frame it appears.
	public abstract class UIButtonVisual : MonoBehaviour
	{
		[Header("References")]
		[Required]
		[SerializeField] private UIButton _button;

		protected UIButton Button => _button;

		private void Reset()
		{
			_button = GetComponentInParent<UIButton>();
		}

		private void OnEnable()
		{
			if (!_button) return;

			_button.OnStateChanged += HandleStateChanged;

			// Snapped rather than animated: a button re-enabled mid-hover would otherwise play its way
			// in from whatever it looked like when it was last switched off.
			Apply(_button.State, true);
		}

		private void OnDisable()
		{
			if (!_button) return;

			_button.OnStateChanged -= HandleStateChanged;
		}

		private void HandleStateChanged(UIButtonState previous, UIButtonState current) => Apply(current, false);

		private void Apply(UIButtonState state, bool instant)
		{
			if (!isActiveAndEnabled) return;

			OnApply(state, instant);
		}

		protected abstract void OnApply(UIButtonState state, bool instant);
	}
}
