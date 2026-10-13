using DG.Tweening;
using UnityEngine;

namespace Game.Runtime.UI.Motion
{
	// Bobs and sways forever, each instance out of step with the next, so a screen of them drifts rather than
	// pulses. Owns this object's anchored position and rotation; keep it off a UIParallaxLayer's object.
	[RequireComponent(typeof(RectTransform))]
	public class UIFloatMotion : MonoBehaviour
	{
		[Tooltip("How far it rises and sinks, in UI units.")]
		[SerializeField] private float _distance = 12f;

		[Tooltip("Seconds for one rise or one sink.")]
		[Min(0.1f)]
		[SerializeField] private float _duration = 2f;

		[Tooltip("How far it rocks either way, in degrees. 0 only bobs.")]
		[SerializeField] private float _swayDegrees = 2f;

		[Tooltip("Seconds for one rock either way.")]
		[Min(0.1f)]
		[SerializeField] private float _swayDuration = 3f;

		[Tooltip("How much each instance's timing differs from the next, as a share of its durations.")]
		[Range(0f, 1f)]
		[SerializeField] private float _variation = 0.25f;

		[SerializeField] private Ease _ease = Ease.InOutSine;

		private RectTransform _rect;
		private Vector2 _restPosition;
		private Quaternion _restRotation;
		private Tween _bob;
		private Tween _sway;

		private void Awake()
		{
			_rect = (RectTransform)transform;
			_restPosition = _rect.anchoredPosition;
			_restRotation = _rect.localRotation;
		}

		private void OnEnable()
		{
			var bobDuration = _duration * (1f + Random.Range(-_variation, _variation));
			_rect.anchoredPosition = _restPosition - new Vector2(0f, _distance * 0.5f);
			_bob = DOTween.To(() => _rect.anchoredPosition.y, SetHeight, _restPosition.y + _distance * 0.5f, bobDuration)
				.SetEase(_ease).SetLoops(-1, LoopType.Yoyo).SetUpdate(true);
			_bob.Goto(Random.value * bobDuration * 2f, true);

			if (_swayDegrees == 0f) return;

			var swayDuration = _swayDuration * (1f + Random.Range(-_variation, _variation));
			var rest = _restRotation.eulerAngles;
			_rect.localRotation = _restRotation * Quaternion.Euler(0f, 0f, -_swayDegrees);
			_sway = _rect.DOLocalRotate(new Vector3(rest.x, rest.y, rest.z + _swayDegrees), swayDuration)
				.SetEase(_ease).SetLoops(-1, LoopType.Yoyo).SetUpdate(true);
			_sway.Goto(Random.value * swayDuration * 2f, true);
		}

		private void SetHeight(float y) => _rect.anchoredPosition = new Vector2(_restPosition.x, y);

		private void OnDisable()
		{
			_sway?.Kill();
			_bob?.Kill();
			_rect.anchoredPosition = _restPosition;
			_rect.localRotation = _restRotation;
		}
	}
}
