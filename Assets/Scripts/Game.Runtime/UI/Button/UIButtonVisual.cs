using DG.Tweening;
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

		[Header("Flash")]
		[Tooltip("Seconds a flash holds the hover look before settling back to the button's real state.")]
		[MinValue(0f)]
		[SerializeField] private float _flashHold = 0.15f;

		protected UIButton Button => _button;

		private Tween _flash;

		// Non-virtual, so a subclass cannot hide it and leave the button unfilled — the whole point of
		// the lookup is that a visual may sit on a child of the button, or on another object entirely,
		// and still find what it draws for. A subclass fills its own references in OnReset.
		private void Reset()
		{
			_button = GetComponentInParent<UIButton>();
			OnReset();
		}

		protected virtual void OnReset()
		{
		}

		private void OnEnable()
		{
			if (!_button) return;

			_button.OnStateChanged += HandleStateChanged;
			_button.OnFlash += HandleFlash;

			// Snapped rather than animated: a button re-enabled mid-hover would otherwise play its way
			// in from whatever it looked like when it was last switched off.
			Apply(_button.State, true);
		}

		private void OnDisable()
		{
			if (_button)
			{
				_button.OnFlash -= HandleFlash;
				_button.OnStateChanged -= HandleStateChanged;
			}

			_flash?.Kill();
			OnDisabled();
		}

		// A subclass that must undo something when switched off does it here. Declaring OnDisable itself would
		// hide this one and leave the button subscription behind, the same trap Reset/OnReset exists for.
		protected virtual void OnDisabled()
		{
		}

		// A state that arrives without having changed is a redraw, not a transition — UIButton forces one
		// when it resets — and there is nothing to animate from, so it snaps. The same reasoning as
		// UIWheelItemView re-applying on bind: only a real change earns the tween.
		private void HandleStateChanged(UIButtonState previous, UIButtonState current)
		{
			_flash?.Kill();
			Apply(current, previous == current);
		}

		// A real state change kills the flash, so a pointer arriving mid-flash is never undone by it.
		private void HandleFlash()
		{
			if (_button.State != UIButtonState.Normal) return;

			_flash?.Kill();
			Apply(UIButtonState.Hovered, false);
			_flash = DOVirtual.DelayedCall(_flashHold, () => Apply(_button.State, false))
				.SetUpdate(true)
				.SetLink(gameObject);
		}

		private void Apply(UIButtonState state, bool instant)
		{
			if (!isActiveAndEnabled) return;

			OnApply(state, instant);
		}

		protected abstract void OnApply(UIButtonState state, bool instant);
	}
}
