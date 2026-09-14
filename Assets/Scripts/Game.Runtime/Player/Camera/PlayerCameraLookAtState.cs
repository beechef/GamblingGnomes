using UnityEngine;

namespace Game.Runtime.Player.Camera
{
	// A shot that is the player turning their head to look at something. There is no virtual camera in it:
	// the view already hangs off the bone the look drives, so pointing the look points the view — and the
	// head really turns, which is what every other seat at the table has to see. A camera swung onto a
	// target beside a head that never moved shows this client one thing and the room another.
	//
	// What to look at is the subclass's own business (PlayerCameraState says why), and it is asked for
	// again whenever the subclass says the answer has moved.
	public abstract class PlayerCameraLookAtState : PlayerCameraState
	{
		[Header("Look")]
		[Tooltip("On, the player may still turn the view themselves and the aim takes it back a moment after they stop. Off, they cannot turn at all — which is what a beat waiting for a click needs, since handing the look back takes the cursor with it.")]
		[SerializeField] private bool _allowManualLook;

		protected override void OnEnter()
		{
			// Set both ways rather than released on exit: the state that comes next says what it wants, so a
			// locked shot handing over to one that allows turning is the next OnEnter and not a release the
			// leaving state has to remember.
			if (Controller && Controller.PlayerController)
				Controller.PlayerController.SetLookInputDisabled(!_allowManualLook);

			Aim();
		}

		protected override void OnExit()
		{
			// The target is dropped here because nothing else knows about one — free flight releases the
			// input, but an aim left standing would go on turning the head through a beat that is over.
			if (Controller && Controller.PlayerController)
				Controller.PlayerController.ClearLookTarget();
		}

		// What this shot is about, right now. Null is a real answer — nobody to look at — and leaves the
		// look where the player last had it rather than snapping anywhere.
		protected abstract Transform ResolveTarget();

		// Called by the subclass when its answer has moved: the turn has passed to somebody else, a seat
		// has filled, a body has spawned.
		protected void Aim()
		{
			if (!IsActive || !Controller || !Controller.PlayerController) return;

			Controller.PlayerController.SetLookTarget(ResolveTarget());
		}
	}
}
