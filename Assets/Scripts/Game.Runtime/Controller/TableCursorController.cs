using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Runtime.Controller
{
	// At a table there is something to point at, so a mouse is free by default and the view is turned by
	// holding a button — the opposite of walking around, where the mouse only ever turns the view.
	//
	// A pad gets no hold at all. It points with the virtual cursor, which is a device of its own, and its
	// two sticks do the pointing and the looking at once — asking a player to hold a shoulder button for
	// the whole hand is asking too much of a control scheme that does not need to choose.
	//
	// It holds a cursor unlock and a pointer request rather than setting either outright: a pause menu
	// opening on top must not be undone when this lets go, and the counters in CursorController are what
	// let the two overlap.
	//
	// Owner only. This lives on the player, so every copy of every player at the table runs it — and a
	// remote one would hold a release on this machine's cursor and answer this machine's hold button on
	// behalf of somebody else's chair. Ownership is not settled until the object spawns, which is why the
	// binding cannot simply live in OnEnable.
	public class TableCursorController : NetworkBehaviour
	{
		[Header("Input")]
		[Tooltip("Held to turn the view on a mouse. A pad ignores it: the stick turns the view whatever this is doing.")]
		[SerializeField] private InputActionReference _holdLookAction;

		private bool _bound;
		private bool _unlockHeld;
		private bool _lookHeld;

		public override void OnNetworkSpawn()
		{
			if (IsOwner) Bind();
		}

		public override void OnNetworkDespawn() => Unbind();

		// Covers being switched off and on again after the object has already spawned, which OnNetworkSpawn
		// cannot: it is raised once.
		private void OnEnable()
		{
			if (IsSpawned && IsOwner) Bind();
		}

		private void OnDisable() => Unbind();

		private void Bind()
		{
			if (_bound) return;

			_bound = true;

			if (_holdLookAction && _holdLookAction.action != null)
			{
				_holdLookAction.action.started += HandleHoldStarted;
				_holdLookAction.action.canceled += HandleHoldCanceled;
				_holdLookAction.action.Enable();
			}

			CursorController.RequestPointer();

			AcquireUnlock();
		}

		private void Unbind()
		{
			if (!_bound) return;

			_bound = false;

			if (_holdLookAction && _holdLookAction.action != null)
			{
				_holdLookAction.action.started -= HandleHoldStarted;
				_holdLookAction.action.canceled -= HandleHoldCanceled;
			}

			// Given back in the reverse order they were taken: leaving with the button still down would
			// strand an unlock nobody can release, and the cursor would never lock again.
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
