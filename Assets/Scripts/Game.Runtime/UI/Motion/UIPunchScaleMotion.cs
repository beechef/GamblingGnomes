using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.UI.Motion
{
	// A swell on demand, for a value that just changed. The rest scale is captured once, so a punch fired
	// mid-punch never leaves the target bigger for good. Put it on art no layout group places.
	public class UIPunchScaleMotion : MonoBehaviour
	{
		[Required]
		[SerializeField] private RectTransform _target;

		[SerializeField] private Vector3 _punch = new(0.3f, 0.3f, 0f);

		[MinValue(0f)]
		[SerializeField] private float _duration = 0.4f;

		[MinValue(0)]
		[SerializeField] private int _vibrato = 6;

		[PropertyRange(0f, 1f)]
		[SerializeField] private float _elasticity = 0.5f;

		private Vector3 _restScale = Vector3.one;
		private Tween _tween;

		private void Reset() => _target = transform as RectTransform;

		private void Awake()
		{
			if (_target) _restScale = _target.localScale;
		}

		private void OnDisable()
		{
			_tween?.Kill();
			if (_target) _target.localScale = _restScale;
		}

		public void Play()
		{
			if (!_target || !isActiveAndEnabled) return;

			_tween?.Kill();
			_target.localScale = _restScale;
			_tween = _target.DOPunchScale(_punch, _duration, _vibrato, _elasticity).SetUpdate(true).SetLink(gameObject);
		}
	}
}
