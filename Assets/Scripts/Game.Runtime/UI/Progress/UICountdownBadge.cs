using DG.Tweening;
using Game.Runtime.Audio;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Runtime.UI.Progress
{
	// A countdown drawn as whole seconds in a frame. Through the last few seconds it turns urgent, and every
	// second that ticks over pops it and, when asked to, plays the warning sound.
	public class UICountdownBadge : MonoBehaviour
	{
		[Header("References")]
		[SerializeField] private TextMeshProUGUI _label;

		[Tooltip("Painted with the label's colour, so the frame goes urgent with the number.")]
		[SerializeField] private Image _frame;

		[Tooltip("What pops each second of the warning. Its scale is the badge's own; nothing else may drive it.")]
		[SerializeField] private RectTransform _popTarget;

		[Header("Warning")]
		[Tooltip("Seconds left at which the badge turns urgent and starts popping, this second included.")]
		[Min(1)]
		[SerializeField] private int _warningSeconds = 5;

		[SerializeField] private Color _normalColor = Color.white;
		[SerializeField] private Color _warningColor = new(0.79f, 0.24f, 0.25f);

		[Tooltip("Played as each warning second ticks over, when the caller asks for it.")]
		[SerializeField] private AudioEvent _tickSound;

		[Header("Pop")]
		[Min(1f)]
		[SerializeField] private float _popScale = 1.35f;

		[Min(0.01f)]
		[SerializeField] private float _popDuration = 0.3f;

		private int _shownSeconds = -1;
		private Vector3 _restScale = Vector3.one;
		private Tween _popTween;

		private void Awake()
		{
			if (_popTarget) _restScale = _popTarget.localScale;
		}

		private void OnDisable()
		{
			_popTween?.Kill();
			if (_popTarget) _popTarget.localScale = _restScale;
			_shownSeconds = -1;
		}

		public void SetTime(float remaining, bool alarm)
		{
			var seconds = Mathf.CeilToInt(Mathf.Max(0f, remaining));
			if (seconds == _shownSeconds) return;

			// Only a second counted down to fires the warning, never one the badge just woke up on.
			var ticked = _shownSeconds > seconds;
			_shownSeconds = seconds;

			if (_label) _label.text = seconds.ToString();

			var warning = seconds <= _warningSeconds;
			Paint(warning ? _warningColor : _normalColor);

			if (!warning || !ticked || seconds <= 0) return;

			Pop();
			if (alarm && _tickSound && AudioManager.Instance) AudioManager.Instance.PlayOneShot(_tickSound, transform.position);
		}

		private void Paint(Color color)
		{
			if (_label) _label.color = color;
			if (_frame) _frame.color = color;
		}

		private void Pop()
		{
			if (!_popTarget) return;

			var half = _popDuration * 0.5f;
			_popTween?.Kill();
			_popTarget.localScale = _restScale;
			_popTween = DOTween.Sequence()
				.Append(_popTarget.DOScale(_restScale * _popScale, half).SetEase(Ease.OutQuad))
				.Append(_popTarget.DOScale(_restScale, half).SetEase(Ease.InQuad))
				.SetLink(gameObject);
		}
	}
}
