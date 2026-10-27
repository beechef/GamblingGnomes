using DG.Tweening;
using Game.Runtime.Controller;
using Game.Runtime.UI.Button;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Runtime.UI.Selection
{
	// Keeps the focused entry of a scrolled list in view, so arrow keys and a pad can walk past the edge. The
	// entry is the content's child holding the focused button, so a row snaps whole even when focus sits on a
	// control inside it. It moves only as far as it has to, and leaves the list alone while a mouse is over it:
	// scrolling under a hovering pointer would hand the hover to the next entry and walk the list away on its own.
	[RequireComponent(typeof(ScrollRect))]
	public class UIScrollToSelection : MonoBehaviour, ICanvasElement, IPointerEnterHandler, IPointerExitHandler
	{
		[Tooltip("Room kept between the focused entry and the edge it was brought in from.")]
		[MinValue(0f)]
		[SerializeField] private float _margin = 8f;

		[MinValue(0f)]
		[SerializeField] private float _duration = 0.15f;

		[SerializeField] private Ease _ease = Ease.OutCubic;

		private ScrollRect _scroll;
		private RectTransform _entry;
		private Tween _move;
		private bool _pointerInside;

		private void Awake()
		{
			_scroll = GetComponent<ScrollRect>();
		}

		private void OnEnable() => UIButton.OnSelected += HandleSelected;

		private void OnDisable()
		{
			UIButton.OnSelected -= HandleSelected;

			_entry = null;
			_pointerInside = false;
			CanvasUpdateRegistry.UnRegisterCanvasElementForRebuild(this);
			KillMove();
		}

		private void OnDestroy()
		{
			CanvasUpdateRegistry.UnRegisterCanvasElementForRebuild(this);
			KillMove();
		}

		public void OnPointerEnter(PointerEventData eventData) => _pointerInside = true;
		public void OnPointerExit(PointerEventData eventData) => _pointerInside = false;

		private void HandleSelected(UIButton button)
		{
			var entry = EntryOf(button.transform);
			if (!entry) return;

			_entry = entry;
			CanvasUpdateRegistry.RegisterCanvasElementForLayoutRebuild(this);
		}

		private RectTransform EntryOf(Transform target)
		{
			var content = _scroll.content;
			if (!content) return null;

			while (target && target.parent != content) target = target.parent;
			return target as RectTransform;
		}

		public void Rebuild(CanvasUpdate executing)
		{
			if (executing != CanvasUpdate.PostLayout) return;
			if (_pointerInside && !InputSchemeController.IsGamepad) return;

			BringIntoView();
		}

		public void LayoutComplete()
		{
		}

		public void GraphicUpdateComplete()
		{
		}

		public bool IsDestroyed() => this == null;

		// Content is pinned at its top, so its y is how far it has scrolled and an entry's y inside it is
		// zero or below.
		private void BringIntoView()
		{
			var content = _scroll.content;
			var viewport = _scroll.viewport ? _scroll.viewport : (RectTransform)_scroll.transform;
			if (!_entry || !content) return;

			var top = content.InverseTransformPoint(_entry.TransformPoint(new Vector3(0f, _entry.rect.yMax))).y + _margin;
			var bottom = content.InverseTransformPoint(_entry.TransformPoint(new Vector3(0f, _entry.rect.yMin))).y - _margin;

			var height = viewport.rect.height;
			var current = content.anchoredPosition.y;
			var target = current;

			if (top > -current) target = -top;
			else if (bottom < -current - height) target = -bottom - height;

			target = Mathf.Clamp(target, 0f, Mathf.Max(0f, content.rect.height - height));
			if (Mathf.Approximately(target, current)) return;

			_scroll.StopMovement();
			KillMove();
			_move = DOTween.To(() => content.anchoredPosition.y,
					y => content.anchoredPosition = new Vector2(content.anchoredPosition.x, y),
					target, _duration)
				.SetEase(_ease)
				.SetUpdate(true)
				.SetLink(gameObject);
		}

		private void KillMove()
		{
			_move?.Kill();
			_move = null;
		}
	}
}
