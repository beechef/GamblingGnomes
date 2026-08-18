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
		[Header("Empty")]
		[Tooltip("Faded out for a slot the wheel has no item for. Faded rather than switched off, because the slot is still travelling — deactivating it would kill the tween carrying it.")]
		[SerializeField] private CanvasGroup _group;

		public T Data { get; private set; }
		public bool IsSelected { get; private set; }
		public bool IsEmpty { get; private set; }

		private RectTransform _rectTransform;
		private Tween _tween;
		private bool _applied;
		private bool _emptyApplied;

		private void Awake()
		{
			_rectTransform = (RectTransform)transform;
		}

		private void OnDestroy()
		{
			KillTween();
		}

		// Repainting the selection look is part of binding, not something only a change of selection does.
		// Every path that hands a slot new data — a rebuild, a recycle — goes through here, and each of them
		// leaves a row that must already look right for the slot it is standing in.
		public void Bind(T data)
		{
			Data = data;

			SetEmpty(false);
			OnBind(data);

			_applied = true;

			if (IsSelected) OnSelect(true);
			else OnUnSelect(true);
		}

		// A slot with nothing to show. It keeps its place in the strip and keeps travelling — a wheel whose
		// slots came and went would jump — but it is not drawn, and OnBind is never called with a default
		// nobody asked a subclass to render.
		public void BindEmpty()
		{
			Data = default;

			SetEmpty(true);
		}

		private void SetEmpty(bool empty)
		{
			if (_emptyApplied && IsEmpty == empty) return;

			IsEmpty = empty;
			_emptyApplied = true;

			if (_group)
			{
				_group.alpha = empty ? 0f : 1f;
				_group.blocksRaycasts = !empty;
			}

			OnEmpty(empty);
		}

		// The first call always reaches the hook, even when the value it is given matches the field's
		// default. A freshly built slot has never drawn itself, so "already unselected" is not the same as
		// "already looks unselected" — skipping it leaves the row wearing whatever the prefab happened to
		// be saved with until the wheel is turned.
		public void SetSelected(bool selected, bool instant = false)
		{
			if (_applied && IsSelected == selected) return;

			IsSelected = selected;
			_applied = true;

			if (selected) OnSelect(instant);
			else OnUnSelect(instant);
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
		protected virtual void OnSelect(bool instant) { }
		protected virtual void OnUnSelect(bool instant) { }

		// For a subclass that has more to do than fade — stopping a loop, releasing a held sound. The base
		// has already hidden the group by the time this runs.
		protected virtual void OnEmpty(bool empty) { }
	}
}
