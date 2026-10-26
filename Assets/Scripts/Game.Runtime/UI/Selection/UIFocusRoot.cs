using System.Collections.Generic;
using Game.Runtime.Controller;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Runtime.UI.Selection
{
	// A place the pad's focus lives: a HUD, a panel, a picker. Roots stack in the order they are shown. The
	// newest one takes focus on a pad (what was chosen there last time, else its first choice), and hiding it
	// hands focus back to whatever had it before, so closing a picker lands on the button that opened it.
	// A confining root keeps pad navigation inside itself while it is the newest: everything outside is taken
	// out of uGUI's automatic navigation (Navigation.Mode.None) and put back when it stops being the newest.
	//
	// Focus is taken in CanvasUpdate.PostLayout, the same "children are switched on and placed" moment
	// UISelectionGroup uses, never from OnEnable, where a parent's children are not active yet.
	public class UIFocusRoot : MonoBehaviour, ICanvasElement
	{
		[Header("Focus")]
		[Tooltip("Where focus lands the first time this root is shown, even while it is greyed out (the waiting room's Start before anyone joins). Left empty, the first pressable selectable underneath in hierarchy order.")]
		[SerializeField] private Selectable _firstSelected;

		[Tooltip("On, showing this root again returns to what was focused in it when it was last left.")]
		[SerializeField] private bool _rememberLastSelected = true;

		[Tooltip("On, the pad cannot navigate out of this root while it is the newest shown; the mouse still can.")]
		[SerializeField] private bool _confineNavigation;

		private static readonly List<UIFocusRoot> Shown = new();
		private static readonly Dictionary<Selectable, Navigation> Blocked = new();
		private static UIFocusRoot _confining;

		public static UIFocusRoot Top => Shown.Count > 0 ? Shown[^1] : null;

		private GameObject _returnTo;
		private Selectable _lastSelected;

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void ResetStatics()
		{
			Shown.Clear();
			Blocked.Clear();
			_confining = null;
		}

		private void OnEnable()
		{
			var previous = Top;
			if (previous) previous.RememberSelection();

			_returnTo = CurrentSelected;
			Shown.Add(this);

			InputSchemeController.OnSchemeChanged += HandleSchemeChanged;
			RequestFocus();
		}

		private void OnDisable()
		{
			InputSchemeController.OnSchemeChanged -= HandleSchemeChanged;
			CanvasUpdateRegistry.UnRegisterCanvasElementForRebuild(this);

			var wasTop = Top == this;
			RememberSelection();
			Shown.Remove(this);

			if (wasTop)
			{
				UpdateConfinement();
				RestoreFocus();
			}

			_returnTo = null;
		}

		private void OnDestroy() => CanvasUpdateRegistry.UnRegisterCanvasElementForRebuild(this);

		private void HandleSchemeChanged(InputScheme scheme)
		{
			if (Top == this) RequestFocus();
		}

		private void RequestFocus() => CanvasUpdateRegistry.RegisterCanvasElementForLayoutRebuild(this);

		public void Rebuild(CanvasUpdate executing)
		{
			if (executing != CanvasUpdate.PostLayout || Top != this) return;

			UpdateConfinement();

			if (!InputSchemeController.IsGamepad) return;

			// A list underneath (UISelectionGroup) may already have placed focus on one of its own entries.
			if (Contains(CurrentSelected) && IsFocusable(CurrentSelected)) return;

			Focus(TargetFromTop());
		}

		public void LayoutComplete()
		{
		}

		public void GraphicUpdateComplete()
		{
		}

		public bool IsDestroyed() => this == null;

		// The previous owner's choice if it is still on screen and pressable, otherwise the newest root's own.
		private void RestoreFocus()
		{
			if (!InputSchemeController.IsGamepad) return;

			var target = IsFocusable(_returnTo) ? _returnTo : TargetFromTop();

			// Nothing left to land on: let go rather than leave focus on a button that has just been hidden.
			if (!target && CurrentSelected && !CurrentSelected.activeInHierarchy)
			{
				EventSystem.current.SetSelectedGameObject(null);
				return;
			}

			Focus(target);
		}

		// The newest root with anything to focus: a root whose buttons are all hidden falls through to the HUD
		// under it. A confining root never does; one with nothing in it (kick picking) holds focus on nothing.
		private static GameObject TargetFromTop()
		{
			for (var i = Shown.Count - 1; i >= 0; i--)
			{
				var root = Shown[i];
				var target = root.PreferredTarget();
				if (target || root._confineNavigation) return target;
			}

			return null;
		}

		private GameObject PreferredTarget()
		{
			if (_rememberLastSelected && _lastSelected && IsFocusable(_lastSelected.gameObject) && Contains(_lastSelected.gameObject))
				return _lastSelected.gameObject;

			if (_firstSelected && _firstSelected.gameObject.activeInHierarchy) return _firstSelected.gameObject;

			foreach (var selectable in GetComponentsInChildren<Selectable>())
			{
				if (IsFocusable(selectable.gameObject)) return selectable.gameObject;
			}

			return null;
		}

		private void RememberSelection()
		{
			var current = CurrentSelected;
			if (current && Contains(current) && current.TryGetComponent(out Selectable selectable)) _lastSelected = selectable;
		}

		private bool Contains(GameObject target) => target && target.transform.IsChildOf(transform);

		private static GameObject CurrentSelected
		{
			get
			{
				var events = EventSystem.current;
				return events ? events.currentSelectedGameObject : null;
			}
		}

		private static bool IsFocusable(GameObject target) =>
			target && target.activeInHierarchy && target.TryGetComponent(out Selectable selectable) && selectable.IsInteractable();

		private static void Focus(GameObject target)
		{
			var events = EventSystem.current;
			if (!events || !target || events.currentSelectedGameObject == target) return;

			events.SetSelectedGameObject(target);
		}

		// Only the newest root confines. Everything blocked is put back first, so a root going away never
		// leaves another root's selectables out of navigation.
		private static void UpdateConfinement()
		{
			var top = Top;
			var confining = top && top._confineNavigation ? top : null;
			if (confining == _confining) return;

			foreach (var pair in Blocked)
			{
				if (pair.Key) pair.Key.navigation = pair.Value;
			}

			Blocked.Clear();
			_confining = confining;

			if (!confining) return;

			foreach (var selectable in Selectable.allSelectablesArray)
			{
				if (!selectable || selectable.transform.IsChildOf(confining.transform)) continue;

				var navigation = selectable.navigation;
				if (navigation.mode == Navigation.Mode.None) continue;

				Blocked[selectable] = navigation;
				navigation.mode = Navigation.Mode.None;
				selectable.navigation = navigation;
			}
		}
	}
}
