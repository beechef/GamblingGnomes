using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Runtime.UI.Progress
{
	// A countdown drawn as a bar and a clock. Domain-agnostic: it is told how much time is left and out of
	// how much, and answers with a fill, an mm:ss reading and an urgent colour near the end. Every clock in
	// the game asks the same question, so they all wear the same widget rather than each view carrying its
	// own fill and its own formatting.
	public class UITimerBar : MonoBehaviour
	{
		[Header("References")]
		[Tooltip("Image Type must be Filled — the bar moves by fillAmount, not by resizing.")]
		[SerializeField] private Image _fill;

		[Tooltip("Optional. Left empty, the bar shows no reading.")]
		[SerializeField] private TextMeshProUGUI _label;

		[Header("Colours")]
		[SerializeField] private Color _normalColor = new(0.83f, 0.76f, 0.60f);

		[Tooltip("Fraction of the countdown left at which the bar turns urgent. 0.25 is the last quarter, whatever the stage is worth — a threshold in seconds would mean something different on a 10-second street and a 60-second one.")]
		[Range(0f, 1f)]
		[SerializeField] private float _warningThresholdPercent = .25f;

		[SerializeField] private Color _warningColor = new(0.90f, 0.30f, 0.30f);

		// Set by whoever owns the clock when the countdown belongs to somebody other than the local player,
		// so the same bar can say "this one is yours" without the view reaching into the Image.
		public Color BarColor
		{
			get => _barColor.HasValue ? _barColor.Value : _normalColor;
			set
			{
				_barColor = value;
				RefreshColor();
			}
		}

		private Color? _barColor;
		private float _remaining;

		// Starts full so a bar nobody has spoken to yet is not already shouting.
		private float _normalized = 1f;

		public void ClearBarColor()
		{
			_barColor = null;
			RefreshColor();
		}

		public void SetTime(float remaining, float normalized)
		{
			_remaining = Mathf.Max(0f, remaining);
			_normalized = Mathf.Clamp01(normalized);

			if (_label)
			{
				var seconds = Mathf.CeilToInt(_remaining);
				_label.text = $"{seconds / 60:00}:{seconds % 60:00}";
			}

			if (_fill) _fill.fillAmount = _normalized;

			RefreshColor();
		}

		// Emptied rather than zeroed: a clock with nothing to count reads as stopped, not as expired.
		public void Clear()
		{
			_remaining = 0f;
			_normalized = 1f;

			if (_label) _label.text = string.Empty;
			if (_fill) _fill.fillAmount = 0f;
		}

		private void RefreshColor()
		{
			if (!_fill) return;

			_fill.color = _normalized <= _warningThresholdPercent ? _warningColor : BarColor;
		}
	}
}
