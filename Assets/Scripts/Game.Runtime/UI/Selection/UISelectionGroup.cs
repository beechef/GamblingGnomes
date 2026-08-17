using System;
using System.Collections.Generic;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Runtime.UI.Selection
{
	// A list of choices with one marker travelling between them. Navigating and confirming are uGUI's own
	// job — the group only follows the EventSystem's focus and slides the pointer to it, so arrow keys, a
	// controller and the mouse arrive through one door and can never disagree.
	//
	// Everything that depends on where the items ended up runs in CanvasUpdate.PostLayout, uGUI's own
	// "layout is settled" pass. That is what makes it reliable: no reading a rect that has not been
	// measured, no waiting a frame and hoping, and no separate path for the first time the list appears.
	public class UISelectionGroup : MonoBehaviour, ICanvasElement
	{
		[Header("References")]
		[Tooltip("The single marker that travels — a hand, an arrow, a glow. Left empty, nothing is drawn and only the item states change.")]
		[SerializeField] private RectTransform _pointer;

		[Tooltip("Left empty, every UISelectionItem underneath this object joins the group, in hierarchy order.")]
		[SerializeField] private List<UISelectionItem> _items = new();

		[Header("Behaviour")]
		[Tooltip("On, the first entry takes focus as the group appears, so the menu is never showing nothing and the first arrow press has somewhere to start from.")]
		[SerializeField] private bool _selectFirstOnEnable = true;

		[Tooltip("On, coming back to this list returns to whatever was chosen when it was last shown — stepping into a sub-screen and back does not lose the player's place. Off, it starts from the first entry every time.")]
		[SerializeField] private bool _restoreSelectionOnEnable = true;

		[Header("Pointer Motion")]
		[Tooltip("Nudge from the item's right edge — positive x moves the pointer further out.")]
		[SerializeField] private Vector2 _pointerOffset = new(28f, 0f);

		[MinValue(0f)]
		[SerializeField] private float _moveDuration = 0.14f;

		[SerializeField] private Ease _ease = Ease.OutCubic;

		public event Action<UISelectionItem> OnSelectionChanged;
		public event Action<UISelectionItem> OnSubmitted;

		public UISelectionItem Selected { get; private set; }

		public IReadOnlyList<UISelectionItem> Items => _items;

		private Tween _move;
		private UISelectionItem _lastSelected;

		// The first entry to arrive after the menu appears is placed, not travelled to: a pointer sliding
		// in from wherever it was left reads as a glitch rather than as a choice being made.
		private bool _snapNextSelection;

		private void Awake()
		{
			if (_items.Count == 0) GetComponentsInChildren(true, _items);
		}

		private void OnEnable()
		{
			foreach (var item in _items)
			{
				if (!item) continue;

				item.OnPointed += Select;
				item.OnSubmitted += HandleSubmitted;
			}

			_snapNextSelection = true;

			// Nothing is decided here on purpose. Activating a parent runs its OnEnable before its children
			// are switched on, so asking now which entries are available answers "none" on every re-show.
			// PostLayout is past all of that.
			RequestPointerUpdate();
		}

		private void OnDisable()
		{
			foreach (var item in _items)
			{
				if (!item) continue;

				item.OnPointed -= Select;
				item.OnSubmitted -= HandleSubmitted;
			}

			// Remembered, then genuinely let go. A group still holding its old choice would read the very
			// same entry arriving again as "nothing changed" and return without marking the button or
			// moving the pointer.
			_lastSelected = Selected;

			if (Selected) Selected.IsSelected = false;
			Selected = null;

			// Hidden rather than left where it stopped: the next time this list opens it may well open on a
			// different entry, and a marker sitting at the old one for a frame is the glitch worth avoiding.
			if (_pointer) _pointer.gameObject.SetActive(false);

			CanvasUpdateRegistry.UnRegisterCanvasElementForRebuild(this);
			KillMove();
		}

		private void OnDestroy()
		{
			CanvasUpdateRegistry.UnRegisterCanvasElementForRebuild(this);
			KillMove();
		}

		// Resolution changes, a longer translation, a font swapping in — all of them move the entries
		// without touching the selection, and all of them arrive here.
		private void OnRectTransformDimensionsChange()
		{
			if (isActiveAndEnabled) RequestPointerUpdate();
		}

		public void Select(UISelectionItem item) => Select(item, _snapNextSelection);

		public void Select(UISelectionItem item, bool instant)
		{
			if (Selected == item) return;

			if (Selected) Selected.IsSelected = false;

			Selected = item;
			_snapNextSelection = instant;

			if (Selected) Selected.IsSelected = true;

			RequestPointerUpdate();
			OnSelectionChanged?.Invoke(Selected);
		}

		private void RequestPointerUpdate() => CanvasUpdateRegistry.RegisterCanvasElementForLayoutRebuild(this);

		// uGUI calls this three times per rebuild; PostLayout is the one where every rect this group reads
		// has its final size for the frame.
		public void Rebuild(CanvasUpdate executing)
		{
			if (executing != CanvasUpdate.PostLayout) return;

			if (!Selected) ApplyInitialSelection();

			PlacePointer();
		}

		public void LayoutComplete()
		{
		}

		public void GraphicUpdateComplete()
		{
		}

		public bool IsDestroyed() => this == null;

		// Where the list starts from: last time's choice if it is still offerable, otherwise the top.
		private void ApplyInitialSelection()
		{
			var target = _restoreSelectionOnEnable ? Selectable(_lastSelected) : null;
			if (!target && _selectFirstOnEnable) target = FirstSelectable();
			if (!target) return;

			// Marked first, then handed to the EventSystem. The other way round relies on uGUI raising
			// OnSelect, and it stays silent when the object it is given already had focus — exactly the
			// case when a sub-screen closes and this list comes back.
			Select(target, true);

			var events = EventSystem.current;
			if (events && events.currentSelectedGameObject != target.gameObject) events.SetSelectedGameObject(target.gameObject);
		}

		private UISelectionItem FirstSelectable()
		{
			foreach (var item in _items)
			{
				var selectable = Selectable(item);
				if (selectable) return selectable;
			}

			return null;
		}

		// An entry remembered from last time may have been switched off or greyed out since, so what comes
		// back has to be checked rather than trusted.
		private UISelectionItem Selectable(UISelectionItem item)
		{
			if (!item || !item.isActiveAndEnabled || !item.Button.IsInteractable) return null;

			return _items.Contains(item) ? item : null;
		}

		private void HandleSubmitted(UISelectionItem item) => OnSubmitted?.Invoke(item);

		private void PlacePointer()
		{
			if (!_pointer) return;

			var shown = Selected != null;
			if (_pointer.gameObject.activeSelf != shown) _pointer.gameObject.SetActive(shown);
			if (!shown) return;

			var target = PointerPositionFor(Selected);

			KillMove();

			if (_snapNextSelection || _moveDuration <= 0f)
			{
				_snapNextSelection = false;
				_pointer.anchoredPosition = target;
				return;
			}

			// Unscaled and linked: a menu is usually the one thing on screen while the game is stopped.
			_move = DOTween.To(() => _pointer.anchoredPosition, position => _pointer.anchoredPosition = position,
					target, _moveDuration)
				.SetEase(_ease)
				.SetUpdate(true)
				.SetLink(gameObject);
		}

		// Read off the laid-out rect rather than stored coordinates: the items are placed by a layout
		// group, so where they actually are is only known once it has run.
		private Vector2 PointerPositionFor(UISelectionItem item)
		{
			var anchor = item.PointerAnchor;
			var parent = (RectTransform)_pointer.parent;

			var edge = anchor.TransformPoint(new Vector3(anchor.rect.xMax, anchor.rect.center.y, 0f));

			return (Vector2)parent.InverseTransformPoint(edge) + _pointerOffset;
		}

		private void KillMove()
		{
			_move?.Kill();
			_move = null;
		}
	}
}
