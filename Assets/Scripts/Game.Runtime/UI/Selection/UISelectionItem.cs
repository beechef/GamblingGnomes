using System;
using Game.Runtime.UI.Button;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.Runtime.UI.Selection
{
	// One choice in a UISelectionGroup. Focus belongs to uGUI: the item reports when the EventSystem
	// selects it and pushes hover into that same focus, so the mouse and the arrow keys can never hold
	// two different opinions about which entry is the current one.
	[RequireComponent(typeof(UIButton))]
	public class UISelectionItem : MonoBehaviour, ISelectHandler
	{
		[Header("References")]
		[Tooltip("Where the group's pointer should aim. Left empty, the item's own rect is used.")]
		[SerializeField] private RectTransform _pointerAnchor;

		public event Action<UISelectionItem> OnPointed;
		public event Action<UISelectionItem> OnSubmitted;

		private UIButton _button;

		public UIButton Button
		{
			get
			{
				if (!_button) _button = GetComponent<UIButton>();
				return _button;
			}
		}

		public RectTransform PointerAnchor => _pointerAnchor ? _pointerAnchor : (RectTransform)transform;

		public bool IsSelected
		{
			get => Button.IsSelected;
			set => Button.IsSelected = value;
		}

		private void OnEnable()
		{
			Button.OnHover += HandleHover;
			Button.OnClick += HandleClick;
		}

		private void OnDisable()
		{
			Button.OnHover -= HandleHover;
			Button.OnClick -= HandleClick;

			// An item switched off cannot stay the marked one, or the pointer would sit over nothing.
			Button.IsSelected = false;
		}

		// Arrow keys, a d-pad, a stick and a mouse click all end here: whatever moved uGUI's focus, this is
		// where the group hears about it, so navigation needs no handling of its own.
		public void OnSelect(BaseEventData eventData) => OnPointed?.Invoke(this);

		// Hover hands focus to the EventSystem rather than marking the item behind its back — navigation
		// then carries on from whatever the mouse last touched instead of jumping back to where the
		// keyboard left off.
		private void HandleHover()
		{
			var events = EventSystem.current;

			if (!events)
			{
				OnPointed?.Invoke(this);
				return;
			}

			if (events.currentSelectedGameObject != gameObject) events.SetSelectedGameObject(gameObject);
		}

		// Both a click and a Submit on the focused entry arrive as onClick — uGUI routes the second into
		// the first — so confirming needs no second path either.
		private void HandleClick() => OnSubmitted?.Invoke(this);
	}
}
