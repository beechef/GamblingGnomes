using DG.Tweening;
using Game.Runtime.Controller;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Runtime.UI.Selection
{
	// Keeps a scrolled list's marked entry in view, so arrow keys and a pad can walk past the edge. It moves
	// only as far as it has to, and leaves the list alone while a mouse is over it: scrolling under a hovering
	// pointer would hand the hover to the next entry and walk the list away on its own.
	[RequireComponent(typeof(ScrollRect))]
	public class UIScrollToSelection : MonoBehaviour, ICanvasElement, IPointerEnterHandler, IPointerExitHandler
	{
		[Required]
		[SerializeField] private UISelectionGroup _selection;

		[Tooltip("Room kept between the marked entry and the edge it was brought in from.")]
		[MinValue(0f)]
		[SerializeField] private float _margin = 8f;

		[MinValue(0f)]
		[SerializeField] private float _duration = 0.15f;

		[SerializeField] private Ease _ease = Ease.OutCubic;

		private ScrollRect _scroll;
		private Tween _move;
		private bool _pointerInside;

		private void Awake()
		{
			_scroll = GetComponent<ScrollRect>();
		}

		private void OnEnable()
		{
			if (_selection) _selection.OnSelectionChanged += HandleSelectionChanged;
		}

		private void OnDisable()
		{
			if (_selection) _selection.OnSelectionChanged -= HandleSelectionChanged;

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

		private void HandleSelectionChanged(UISelectionItem item)
		{
			if (item) CanvasUpdateRegistry.RegisterCanvasElementForLayoutRebuild(this);
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
			var item = _selection ? _selection.Selected : null;
			var content = _scroll.content;
			var viewport = _scroll.viewport ? _scroll.viewport : (RectTransform)_scroll.transform;
			if (!item || !content) return;

			var rect = (RectTransform)item.transform;
			var top = content.InverseTransformPoint(rect.TransformPoint(new Vector3(0f, rect.rect.yMax))).y + _margin;
			var bottom = content.InverseTransformPoint(rect.TransformPoint(new Vector3(0f, rect.rect.yMin))).y - _margin;

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
