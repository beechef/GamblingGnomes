using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.Runtime.UI.Motion
{
	// Leans toward wherever the pointer rests on this element, as if pressing that corner in. Sits on the element
	// that takes the pointer and turns a separate target, so the hit area stays flat and never flickers out from
	// under the pointer. Without a target it only reports the lean, for UIParallaxLayers to slide by.
	[DisallowMultipleComponent]
	[RequireComponent(typeof(RectTransform))]
	public class UIPointerTilt : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler
	{
		[Tooltip("What turns: a child holding the visuals, never this object. Empty turns nothing.")]
		[SerializeField] private Transform _target;

		[Tooltip("How far the lean goes with the pointer at an edge, in degrees.")]
		[Range(0f, 45f)]
		[SerializeField] private float _maxDegrees = 12f;

		[Tooltip("Scales how far every UIParallaxLayer under this slides: 0 flat, 1 as each layer's depth says.")]
		[Min(0f)]
		[SerializeField] private float _parallaxStrength = 1f;

		[Tooltip("Seconds to catch up with the pointer; restarted as it moves, so this is the follow lag.")]
		[Min(0.01f)]
		[SerializeField] private float _duration = 0.3f;

		[SerializeField] private Ease _ease = Ease.OutQuad;

		public float ParallaxStrength => _parallaxStrength;

		public Quaternion Tilt { get; private set; } = Quaternion.identity;

		public event Action<Quaternion> OnTilted;

		private RectTransform _rect;
		private Quaternion _targetRest;
		private Vector2 _current;
		private Tween _tween;

		private void Awake()
		{
			_rect = (RectTransform)transform;
			if (_target) _targetRest = _target.localRotation;
		}

		private void OnDisable()
		{
			_tween?.Kill();
			_current = Vector2.zero;
			Apply(_current);
		}

		public void OnPointerEnter(PointerEventData eventData) => Aim(eventData);

		public void OnPointerMove(PointerEventData eventData) => Aim(eventData);

		public void OnPointerExit(PointerEventData eventData) => EaseTo(Vector2.zero);

		private void Aim(PointerEventData eventData)
		{
			var rect = _rect.rect;
			if (rect.width <= 0f || rect.height <= 0f) return;
			if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_rect, eventData.position, eventData.enterEventCamera, out var local)) return;

			var normalized = (local - rect.center) / (rect.size * 0.5f);
			EaseTo(new Vector2(Mathf.Clamp(normalized.x, -1f, 1f), Mathf.Clamp(normalized.y, -1f, 1f)));
		}

		private void EaseTo(Vector2 goal)
		{
			_tween?.Kill();
			_tween = DOTween.To(() => _current, Apply, goal, _duration).SetEase(_ease).SetUpdate(true).SetLink(gameObject);
		}

		// Pointer at the top presses the top away from the viewer; at the right, the right.
		private void Apply(Vector2 lean)
		{
			_current = lean;
			Tilt = Quaternion.Euler(lean.y * _maxDegrees, -lean.x * _maxDegrees, 0f);
			if (_target) _target.localRotation = _targetRest * Tilt;
			OnTilted?.Invoke(Tilt);
		}
	}
}
