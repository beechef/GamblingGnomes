using Sirenix.OdinInspector;
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

		[Tooltip("How long the view holds where it is before a new answer is aimed at — entering this shot and every re-aim inside it. A head that swings the instant the turn moves reads as a cut; a beat of delay reads as somebody looking up at whoever just started deciding.")]
		[MinValue(0f)]
		[SerializeField] private float _aimDelay = 0.5f;

		private float _aimAt;
		private bool _aimScheduled;
		private bool _waitingForTarget;

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
			_aimScheduled = false;
			_waitingForTarget = false;

			// The target is dropped here because nothing else knows about one — free flight releases the
			// input, but an aim left standing would go on turning the head through a beat that is over.
			if (Controller && Controller.PlayerController)
				Controller.PlayerController.ClearLookTarget();
		}

		// What this shot is about, right now. Null is a real answer — nobody to look at — and leaves the
		// look where the player last had it rather than snapping anywhere.
		protected abstract Transform ResolveTarget();

		// Called by the subclass when its answer has moved: the turn has passed to somebody else, a seat
		// has filled, a body has spawned. It schedules the swing rather than making it — what the shot is
		// about has changed, and how soon the head follows is the shot's own setting.
		protected void Aim()
		{
			if (!IsActive) return;

			_aimScheduled = true;
			_aimAt = Time.unscaledTime + _aimDelay;
		}

		private void LateUpdate()
		{
			if (!IsActive) return;

			if (_aimScheduled)
			{
				if (Time.unscaledTime < _aimAt) return;

				_aimScheduled = false;
				ApplyAim();
				return;
			}

			// Nothing to look at *yet* is not the same answer as nothing to look at. A state is entered the
			// frame its stage id arrives, which is routinely before the seat index, the turn or the other
			// player's body have replicated — and the subclasses only re-aim from OnValueChanged, so a value
			// that was already set when this entered raises no event and the shot keeps whatever angle the
			// player happened to be holding. That is the same state coming out at a different angle every
			// time, and it is the late-join rule in a different coat: read the current value on arrival
			// rather than waiting for a change that has already happened.
			//
			// Retried without the delay, because the wait has already been spent: the pause is for holding
			// the view on the beat that is ending, not for the frames a target takes to replicate.
			if (_waitingForTarget) ApplyAim();
		}

		private void ApplyAim()
		{
			if (!Controller || !Controller.PlayerController) return;

			var target = ResolveTarget();

			Controller.PlayerController.SetLookTarget(target);

			_waitingForTarget = !target;
		}
	}
}
