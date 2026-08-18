using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.Runtime.UI.Button
{
	// The button as logic only: it knows whether it is hovered, held or refusing input, and says so. It
	// holds no Image, no colour and no tween — a UIButtonVisual listens and draws the answer, so how a
	// button looks can be changed, restyled or animated without touching what it does.
	[RequireComponent(typeof(UnityEngine.UI.Button))]
	public class UIButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
	{
		public event Action OnClick;
		public event Action OnHover;
		public event Action OnUnHover;

		// Previous and current, so a visual can animate the transition rather than only the destination.
		public event Action<UIButtonState, UIButtonState> OnStateChanged;

		private UnityEngine.UI.Button _button;
		private bool _initialized;
		private bool _hovering;
		private bool _pressed;

		public UIButtonState State { get; private set; } = UIButtonState.Normal;

		// Set by whatever owns the choice — a UISelectionGroup, a tab strip — not by the pointer. It
		// survives the mouse leaving, which is the difference between being hovered and being chosen.
		public bool IsSelected
		{
			get => _selected;
			set
			{
				if (_selected == value) return;

				_selected = value;
				RefreshState();
			}
		}

		private bool _selected;

		// Held down by something that is not the pointer — a key bound to this button. It resolves at the
		// same priority as a pointer press, so a key and a click are indistinguishable to every visual.
		public bool IsPressedByKey
		{
			get => _keyPressed;
			set
			{
				if (_keyPressed == value) return;

				_keyPressed = value;
				RefreshState();
			}
		}

		private bool _keyPressed;

		public bool IsInteractable
		{
			get
			{
				if (!_initialized) Initialize();
				return _button.interactable;
			}
			set
			{
				if (!_initialized) Initialize();
				if (_button.interactable == value) return;

				_button.interactable = value;

				// A button that stops answering also stops being hovered — the pointer never leaves a
				// button that was disabled underneath it, so the exit event never arrives.
				if (!value) ClearPointer();

				RefreshState();
			}
		}

		private void Awake()
		{
			Initialize();
		}

		private void OnEnable()
		{
			// Re-shown under the pointer or not, the button starts from a state it can prove.
			ResetState();
		}

		private void OnDisable()
		{
			ClearPointer();
			RefreshState();
		}

		private void OnDestroy()
		{
			DeInitialize();
		}

		private void Initialize()
		{
			if (_initialized) return;

			_button = GetComponent<UnityEngine.UI.Button>();
			_button.onClick.AddListener(Click);
			_initialized = true;

			RefreshState();
		}

		private void DeInitialize()
		{
			if (!_initialized) return;

			_initialized = false;
			_button.onClick.RemoveListener(Click);
		}

		// Clicked by something that is not the pointer — a key bound to this button, a tutorial driving it.
		// It runs the same guard and raises the same event, so nothing downstream can tell the two apart,
		// and callers never have to reach past this component to the uGUI Button underneath.
		public void Submit() => Click();

		// Puts the button back to rest and redraws it, whether or not the answer has changed. Anything that
		// takes the pointer away mid-press calls this — opening a screen, closing a panel, starting a load —
		// because uGUI sends the release to whoever received the press, and a button that never hears it is
		// left holding _pressed with nothing coming to clear it. The redraw is forced for the same reason
		// UIWheelItemView re-applies on bind: a visual left mid-tween has not changed state, so a guard
		// written as "nothing changed, nothing to do" is exactly the thing that keeps it stuck.
		public void ResetState()
		{
			ClearPointer();
			RefreshState(true);
		}

		private void Click()
		{
			if (!IsInteractable) return;
			OnClick?.Invoke();

			// A click is the moment a screen tends to change underneath the pointer, and uGUI delivers the
			// release to whoever took the press — so a button whose handler opened something never hears it
			// and is left holding the press. A key still down is left alone: holding the key must keep
			// holding the button, which is the bracket UIButtonHotkey exists to draw.
			// Checked for survival first, because the handler above is allowed to destroy this button.
			if (this && !_keyPressed) ResetState();
		}

		public void OnPointerEnter(PointerEventData eventData)
		{
			if (!IsInteractable) return;

			_hovering = true;
			RefreshState();

			OnHover?.Invoke();
		}

		public void OnPointerExit(PointerEventData eventData)
		{
			if (!_hovering && !_pressed) return;

			_hovering = false;
			_pressed = false;
			RefreshState();

			OnUnHover?.Invoke();
		}

		public void OnPointerDown(PointerEventData eventData)
		{
			if (!IsInteractable) return;

			_pressed = true;
			RefreshState();
		}

		public void OnPointerUp(PointerEventData eventData)
		{
			if (!_pressed) return;

			_pressed = false;
			RefreshState();
		}

		private void ClearPointer()
		{
			_hovering = false;
			_pressed = false;

			// A key held as the button is switched off would otherwise still be held when it comes back.
			_keyPressed = false;
		}

		// force says "redraw even if the answer is the same". Ordinary input changes leave it off, so a
		// visual only animates on a real transition; a reset turns it on, because there the whole point is
		// a picture that no longer matches a state which never moved.
		private void RefreshState(bool force = false)
		{
			// Lazily initialised so a button asked about itself before Awake — by an editor tool, or by
			// a group selecting its first entry as it is built — answers with its real state instead of
			// claiming to be disabled.
			if (!_initialized) Initialize();

			var previous = State;
			var current = Resolve();

			if (previous == current && !force) return;

			State = current;
			OnStateChanged?.Invoke(previous, current);
		}

		private UIButtonState Resolve()
		{
			if (!_initialized || !_button.interactable) return UIButtonState.Disabled;
			if (_pressed || _keyPressed) return UIButtonState.Pressed;
			if (_selected) return UIButtonState.Selected;
			if (_hovering) return UIButtonState.Hovered;

			return UIButtonState.Normal;
		}
	}
}
