using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Runtime.UI
{
	// For a uGUI control that is not a UIButton — a slider, a toggle, an input field, a scrollbar: says when the
	// pointer is over it while it answers, the same way UIButton.OnPointerOverChanged does, so the screen-wide
	// reactions (the cursor's look) treat every control alike without the control knowing they exist.
	[RequireComponent(typeof(Selectable))]
	public class UISelectableHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler, ISubmitHandler
	{
		public static event Action<UISelectableHover, bool> OnPointerOverChanged;

		// A toggle flipped by the player, by click or submit, never by code setting its value, for what answers
		// it like a button press (the click sound).
		public static event Action<UISelectableHover> OnToggleFlipped;

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void ResetStatics()
		{
			OnPointerOverChanged = null;
			OnToggleFlipped = null;
		}

		private Selectable _selectable;
		private bool _over;

		private void Awake() => _selectable = GetComponent<Selectable>();

		// The pointer never leaves a control switched off underneath it, so the exit is raised here.
		private void OnDisable() => SetOver(false);

		public void OnPointerEnter(PointerEventData eventData) => SetOver(_selectable.IsInteractable());

		public void OnPointerExit(PointerEventData eventData) => SetOver(false);

		public void OnPointerClick(PointerEventData eventData)
		{
			if (eventData.button == PointerEventData.InputButton.Left) RaiseToggleFlipped();
		}

		public void OnSubmit(BaseEventData eventData) => RaiseToggleFlipped();

		private void RaiseToggleFlipped()
		{
			if (_selectable is Toggle && _selectable.IsInteractable()) OnToggleFlipped?.Invoke(this);
		}

		private void SetOver(bool over)
		{
			if (_over == over) return;

			_over = over;
			OnPointerOverChanged?.Invoke(this, over);
		}
	}
}
