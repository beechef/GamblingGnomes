using Game.Runtime.UI.Button;
using TMPro;
using UnityEngine;

namespace Game.Runtime.UI.Config
{
	// A number stepped between two buttons. Int and float rows only differ in step size and formatting,
	// so the wiring lives here once and each declares its own arithmetic.
	public abstract class UIMatchConfigStepperRow : UIMatchConfigRow
	{
		[Header("Stepper")]
		[SerializeField] private UIButton _minusButton;
		[SerializeField] private UIButton _plusButton;
		[SerializeField] private TextMeshProUGUI _valueLabel;

		private void Awake()
		{
			if (_minusButton) _minusButton.OnClick += HandleMinus;
			if (_plusButton) _plusButton.OnClick += HandlePlus;
		}

		private void OnDestroy()
		{
			if (_minusButton) _minusButton.OnClick -= HandleMinus;
			if (_plusButton) _plusButton.OnClick -= HandlePlus;
		}

		protected abstract float StepSize { get; }

		protected abstract float RangeMin { get; }

		protected abstract float RangeMax { get; }

		protected abstract string FormatValue(float value);

		protected override void OnRefresh()
		{
			var value = Entry != null && Access != null ? Access.GetValue(Entry) : 0f;

			if (_valueLabel) _valueLabel.text = FormatValue(value);

			// Dimmed at the edge of the range the way a stack that cannot cover a bet is dimmed — the
			// button is there, it just has nothing left to offer in that direction.
			if (_minusButton) _minusButton.IsInteractable = IsEditable && value > RangeMin;
			if (_plusButton) _plusButton.IsInteractable = IsEditable && value < RangeMax;
		}

		private void HandleMinus() => StepBy(-1);

		private void HandlePlus() => StepBy(1);

		private void StepBy(int direction)
		{
			if (!IsEditable || Entry == null || Access == null) return;

			Access.SetValue(Entry, Entry.ClampValue(Access.GetValue(Entry) + direction * StepSize));
			Refresh();
		}
	}
}
