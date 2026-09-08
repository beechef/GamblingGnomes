using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Runtime.Controller
{
	// At a table there is something to point at, so a mouse is free by default and the view is turned by
	// holding a button — the opposite of walking around, where the mouse only ever turns the view.
	//
	// A pad is the other way round again, and gets no hold at all. It points with the virtual cursor,
	// which is a device of its own and needs nothing unlocked, so the hardware cursor can stay away and
	// the left stick turns the view on its own. Asking a player to hold a shoulder button to look around
	// is asking them to hold it for the whole hand.
	//
	// It holds an unlock rather than toggling the lock itself: a pause menu opening on top must not be
	// undone when this releases, and the counter in CursorController is what lets the two overlap.
	public class TableCursorController : MonoBehaviour
	{
		[Header("Input")]
		[Tooltip("Held to turn the view on a mouse. A pad ignores it: the stick turns the view whatever this is doing.")]
		[SerializeField] private InputActionReference _holdLookAction;

		private bool _unlockHeld;
		private bool _lookHeld;

		private void OnEnable()
		{
			if (_holdLookAction && _holdLookAction.action != null)
			{
				_holdLookAction.action.started += HandleHoldStarted;
				_holdLookAction.action.canceled += HandleHoldCanceled;
				_holdLookAction.action.Enable();
			}

			InputSchemeController.OnSchemeChanged += HandleSchemeChanged;

			RefreshUnlock();
		}

		private void OnDisable()
		{
			if (_holdLookAction && _holdLookAction.action != null)
			{
				_holdLookAction.action.started -= HandleHoldStarted;
				_holdLookAction.action.canceled -= HandleHoldCanceled;
			}

			InputSchemeController.OnSchemeChanged -= HandleSchemeChanged;

			// Both are given back, and in the reverse order they were taken: leaving with the button still
			// down would strand an unlock nobody can release, and the cursor would never lock again.
			_lookHeld = false;
			ReleaseUnlock();
		}

		private void HandleSchemeChanged(InputScheme scheme) => RefreshUnlock();

		private void HandleHoldStarted(InputAction.CallbackContext context)
		{
			_lookHeld = true;
			RefreshUnlock();
		}

		private void HandleHoldCanceled(InputAction.CallbackContext context)
		{
			_lookHeld = false;
			RefreshUnlock();
		}

		// One place decides, so the hold and the device in hand cannot each reach a different answer.
		private void RefreshUnlock()
		{
			if (_lookHeld || InputSchemeController.IsGamepad) ReleaseUnlock();
			else AcquireUnlock();
		}

		private void AcquireUnlock()
		{
			if (_unlockHeld) return;

			_unlockHeld = true;
			CursorController.RequestUnlock();
		}

		private void ReleaseUnlock()
		{
			if (!_unlockHeld) return;

			_unlockHeld = false;
			CursorController.ReleaseUnlock();
		}
	}
}
