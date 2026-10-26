using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.Runtime.UI.Input
{
	// While a text field is open for typing, Back and Escape only close the field; the next press closes the
	// panel. It holds a place on UIEscapeStack that does nothing when dismissed: TMP_InputField's own OnCancel
	// (the input module sends it the same press, after the action callbacks that dismiss the stack) is what
	// closes the field. Closing it here instead would make that OnCancel reopen it on the next update.
	//
	// TMP raises no event when a field opens, so it is caught where a field opens: a click, a Submit, or
	// focus on a field that opens on focus.
	[RequireComponent(typeof(TMP_InputField))]
	public class UIInputFieldEscape : MonoBehaviour, IPointerClickHandler, ISubmitHandler
	{
		private TMP_InputField _field;
		private bool _held;

		private void Awake()
		{
			_field = GetComponent<TMP_InputField>();
		}

		private void OnEnable()
		{
			_field.onSelect.AddListener(HandleFocused);
			_field.onEndEdit.AddListener(HandleClosed);
			_field.onDeselect.AddListener(HandleClosed);
		}

		private void OnDisable()
		{
			_field.onDeselect.RemoveListener(HandleClosed);
			_field.onEndEdit.RemoveListener(HandleClosed);
			_field.onSelect.RemoveListener(HandleFocused);

			Release();
		}

		public void OnPointerClick(PointerEventData eventData) => Hold();

		public void OnSubmit(BaseEventData eventData) => Hold();

		private void HandleFocused(string text)
		{
			if (_field.shouldActivateOnSelect) Hold();
		}

		private void HandleClosed(string text) => Release();

		private void Hold()
		{
			if (!_field.interactable) return;

			_held = true;
			UIEscapeStack.Push(HandleEscape);
		}

		private void Release()
		{
			if (!_held) return;

			_held = false;
			UIEscapeStack.Remove(HandleEscape);
		}

		private void HandleEscape() => _held = false;
	}
}
