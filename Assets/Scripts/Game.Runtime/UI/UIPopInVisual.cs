using System;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.UI
{
	// A panel that pops up each time it is switched on: it swells out of a smaller scale and fades in.
	// Presentation only — whoever shows the panel just activates it, and this draws the arrival.
	//
	// Where it grows from is the target's pivot, authored on the prefab: a board opened from a corner button
	// puts its pivot in that corner. Put it on art inside a panel rather than on a rect a layout group
	// places, so the scale never fights the layout; the rest scale is captured once so a re-show mid-tween
	// cannot shrink it for good.
	public class UIPopInVisual : MonoBehaviour
	{
		[Header("References")]
		[Required]
		[SerializeField] private RectTransform _target;

		[Tooltip("Optional. Faded in alongside the scale.")]
		[SerializeField] private CanvasGroup _canvasGroup;

		[Header("Motion")]
		[MinValue(0f)]
		[SerializeField] private float _fromScale = 0.6f;

		[MinValue(0f)]
		[SerializeField] private float _duration = 0.3f;

		[SerializeField] private Ease _scaleEase = Ease.OutBack;

		[MinValue(0f)]
		[SerializeField] private float _fadeDuration = 0.15f;

		[Header("Out")]
		[Tooltip("How long the panel fades to nothing when it is asked to go. Needs the CanvasGroup; zero goes at once.")]
		[MinValue(0f)]
		[SerializeField] private float _fadeOutDuration = 0.2f;

		[SerializeField] private Ease _fadeOutEase = Ease.OutQuad;

		[Tooltip("On, going away plays the pop-in backwards: the target shrinks back to the from-scale toward its pivot while it fades. Off, it only fades.")]
		[SerializeField] private bool _popOut;

		[MinValue(0f)]
		[SerializeField] private float _popOutDuration = 0.2f;

		[SerializeField] private Ease _popOutEase = Ease.InBack;

		private Vector3 _restScale = Vector3.one;
		private Sequence _sequence;

		public bool IsHiding { get; private set; }

		private void Reset()
		{
			_target = transform as RectTransform;
			_canvasGroup = GetComponent<CanvasGroup>();
		}

		private void Awake()
		{
			if (_target) _restScale = _target.localScale;
		}

		private void OnEnable()
		{
			if (!_target) return;

			Kill();

			_target.localScale = _restScale * _fromScale;
			if (_canvasGroup) _canvasGroup.alpha = 0f;

			// Unscaled and linked, like every other UI tween here.
			_sequence = DOTween.Sequence()
				.Append(_target.DOScale(_restScale, _duration).SetEase(_scaleEase))
				.SetUpdate(true)
				.SetLink(gameObject);

			if (_canvasGroup)
			{
				_sequence.Insert(0f, DOTween.To(() => _canvasGroup.alpha, value => _canvasGroup.alpha = value, 1f, _fadeDuration));
			}
		}

		// The way out is asked for rather than drawn on OnDisable, because a switched-off object draws nothing:
		// the caller switches the panel off in onHidden, once the fade (and the pop-out, if on) has finished. Switching it off earlier
		// cancels the fade, which is also how a panel reopened mid-fade gets its pop-in back.
		public void Hide(Action onHidden)
		{
			Kill();

			var fades = _canvasGroup && _fadeOutDuration > 0f;
			var shrinks = _popOut && _target && _popOutDuration > 0f;

			if (!isActiveAndEnabled || (!fades && !shrinks))
			{
				onHidden?.Invoke();
				return;
			}

			IsHiding = true;

			// A panel on its way out is already gone as far as the player is concerned: a click landing on it
			// mid-fade would act on a screen that has been dismissed. Given back in OnDisable.
			if (_canvasGroup)
			{
				_canvasGroup.blocksRaycasts = false;
				_canvasGroup.interactable = false;
			}

			_sequence = DOTween.Sequence();

			if (shrinks) _sequence.Insert(0f, _target.DOScale(_restScale * _fromScale, _popOutDuration).SetEase(_popOutEase));
			if (fades) _sequence.Insert(0f, DOTween.To(() => _canvasGroup.alpha, value => _canvasGroup.alpha = value, 0f, _fadeOutDuration).SetEase(_fadeOutEase));

			_sequence
				.OnComplete(() =>
				{
					IsHiding = false;
					onHidden?.Invoke();
				})
				.SetUpdate(true)
				.SetLink(gameObject);
		}

		private void OnDisable()
		{
			Kill();
			IsHiding = false;

			if (_target) _target.localScale = _restScale;
			if (!_canvasGroup) return;

			_canvasGroup.alpha = 1f;
			_canvasGroup.blocksRaycasts = true;
			_canvasGroup.interactable = true;
		}

		private void Kill()
		{
			_sequence?.Kill();
			_sequence = null;
		}
	}
}
