using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Runtime.Controller
{
	// At a table there is something to point at, so the cursor is free by default and the view is turned
	// by holding a button — the opposite of walking around, where the mouse only ever turns the view.
	//
	// It holds an unlock the whole time it is enabled rather than toggling the lock itself: a pause menu
	// opening on top must not be undone when this releases, and the counter in CursorController is what
	// lets the two overlap. Holding the button gives that one unlock back for as long as it is down.
	public class TableCursorController : MonoBehaviour
	{
		[Header("Input")]
		[Tooltip("Held to turn the view. Released, the cursor comes back for pointing at the table.")]
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

			CursorController.RequestPointer();

			AcquireUnlock();
		}

		private void OnDisable()
		{
			if (_holdLookAction && _holdLookAction.action != null)
			{
				_holdLookAction.action.started -= HandleHoldStarted;
				_holdLookAction.action.canceled -= HandleHoldCanceled;
			}

			// Both are given back, and in the reverse order they were taken: leaving with the button still
			// down would strand an unlock nobody can release, and the cursor would never lock again.
			_lookHeld = false;
			ReleaseUnlock();

			CursorController.ReleasePointer();
		}

		private void HandleHoldStarted(InputAction.CallbackContext context)
		{
			_lookHeld = true;
			ReleaseUnlock();
		}

		private void HandleHoldCanceled(InputAction.CallbackContext context)
		{
			_lookHeld = false;
			ReleaseUnlock();

			CursorController.ReleasePointer();
			AcquireUnlock();
		}

		private void AcquireUnlock()
		{
			if (_unlockHeld || _lookHeld) return;

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
