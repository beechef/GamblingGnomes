using DG.Tweening;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Runtime.UI.Progress
{
	// A filled bar and, if it is given one, a label for the number. Domain-agnostic on purpose: loading,
	// a stage clock, a stack of chips — whatever is being counted, this only knows a fraction between
	// zero and one and how long to take getting there.
	public class UIProgressBar : MonoBehaviour
	{
		[Header("References")]
		[Required]
		[Tooltip("Image Type must be Filled — the bar moves by fillAmount, not by resizing.")]
		[SerializeField] private Image _fill;

		[Tooltip("Optional. Left empty, the bar shows no number.")]
		[SerializeField] private TextMeshProUGUI _label;

		[Header("Motion")]
		[Tooltip("Seconds to travel to a new value. Zero snaps, which is what a bar going backwards should do.")]
		[MinValue(0f)]
		[SerializeField] private float _duration = 0.25f;

		[SerializeField] private Ease _ease = Ease.OutQuad;

		[Header("Label")]
		[Tooltip("{0} is the percentage, already rounded.")]
		[SerializeField] private string _format = "{0}%";

		private Tween _tween;

		public float Progress { get; private set; }

		private void OnDisable()
		{
			KillTween();
		}

		private void OnDestroy()
		{
			KillTween();
		}

		public void SetProgress(float value, bool instant = false) => SetProgress(value, instant ? 0f : _duration);

		// Takes the travel time from the caller when the wait itself decides it — a bar given a second to
		// reach the end should spend the second arriving rather than snapping and waiting there.
		public void SetProgress(float value, float duration)
		{
			var target = Mathf.Clamp01(value);
			Progress = target;

			KillTween();

			if (!_fill)
			{
				RefreshLabel(target);
				return;
			}

			if (duration <= 0f || !isActiveAndEnabled)
			{
				_fill.fillAmount = target;
				RefreshLabel(target);
				return;
			}

			// Unscaled: a loading bar keeps moving while the game itself is stopped, which is exactly
			// when a loading bar is on screen.
			_tween = DOTween.To(() => _fill.fillAmount, value2 =>
				{
					_fill.fillAmount = value2;
					RefreshLabel(value2);
				}, target, duration)
				.SetEase(_ease)
				.SetUpdate(true)
				.SetLink(gameObject);
		}

		private void RefreshLabel(float value)
		{
			if (!_label) return;

			_label.text = string.Format(_format, Mathf.RoundToInt(value * 100f));
		}

		private void KillTween()
		{
			_tween?.Kill();
			_tween = null;
		}
	}
}
