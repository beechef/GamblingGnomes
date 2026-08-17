using DG.Tweening;
using UnityEngine;

namespace Game.Runtime.UI.Wheel
{
	// One slot on the wheel. It knows how to travel to an anchor and whether it is the one in the middle;
	// what it looks like while it does is the subclass's business, through OnBind/OnSelect/OnUnSelect —
	// the same template-method split the rest of the UI uses.
	[RequireComponent(typeof(RectTransform))]
	public class UIWheelItemView<T> : MonoBehaviour
	{
		public T Data { get; private set; }
		public bool IsSelected { get; private set; }

		private RectTransform _rectTransform;
		private Tween _tween;

		private void Awake()
		{
			_rectTransform = (RectTransform)transform;
		}

		private void OnDestroy()
		{
			KillTween();
		}

		public void Bind(T data)
		{
			Data = data;

			OnBind(data);
		}

		public void Select()
		{
			if (IsSelected) return;

			IsSelected = true;
			OnSelect();
		}

		public void UnSelect()
		{
			if (!IsSelected) return;

			IsSelected = false;
			OnUnSelect();
		}

		// Used by the slot being recycled around the back: it has just been given different data and must
		// appear at the far end already, not slide across the whole wheel to get there.
		public void SnapTo(RectTransform anchor)
		{
			KillTween();

			if (!_rectTransform) _rectTransform = (RectTransform)transform;

			_rectTransform.localPosition = anchor.localPosition;
			_rectTransform.localRotation = anchor.localRotation;
			_rectTransform.localScale = anchor.localScale;
		}

		public Tween TweenTo(RectTransform anchor, float duration, Ease ease)
		{
			KillTween();

			if (!_rectTransform) _rectTransform = (RectTransform)transform;

			// Position, rotation and scale together, so an anchor can describe a slot that is further away,
			// smaller and tilted all at once and the motion between two of them stays one movement.
			_tween = DOTween.Sequence()
				.Join(_rectTransform.DOLocalMove(anchor.localPosition, duration))
				.Join(_rectTransform.DOLocalRotateQuaternion(anchor.localRotation, duration))
				.Join(_rectTransform.DOScale(anchor.localScale, duration))
				.SetEase(ease)
				.SetUpdate(true)
				.SetLink(gameObject);

			return _tween;
		}

		public void KillTween()
		{
			_tween?.Kill();
			_tween = null;
		}

		protected virtual void OnBind(T data) { }
		protected virtual void OnSelect() { }
		protected virtual void OnUnSelect() { }
	}
}
