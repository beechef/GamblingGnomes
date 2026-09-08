using UnityEngine;

namespace Game.Runtime.Player.Camera
{
	// One thing the view can be doing, as a component on its own object. A state is not a row in a list
	// on the controller because states differ in *behaviour*, not in configuration: free flight hands the
	// look back to the player, a shot swaps the live virtual camera, and whatever comes next will do
	// something neither of them does. Adding one is a new subclass and a new child object.
	//
	// Enter and Exit are not virtual. The controller owns entering and leaving — the invariants about
	// what is active and what it is aimed at live here, once — and a subclass fills in OnEnter/OnExit,
	// which it cannot skip by forgetting to call base.
	public abstract class PlayerCameraState : MonoBehaviour
	{
		public PlayerCameraController Controller { get; private set; }

		// What this state was asked to look at, if anything. Free flight ignores it; a shot aims at it.
		public Transform Target { get; private set; }

		public bool IsActive { get; private set; }

		public void Initialize(PlayerCameraController controller)
		{
			Controller = controller;
			OnInitialize();
		}

		public void Enter(Transform target)
		{
			if (IsActive) { Retarget(target); return; }

			Target = target;
			IsActive = true;
			OnEnter();
		}

		public void Exit()
		{
			if (!IsActive) return;

			IsActive = false;
			OnExit();
			Target = null;
		}

		// The same state asked for again with something else to look at. A shot that is already live
		// swings onto the new target rather than being torn down and rebuilt, which would blend the view
		// out and back for no reason anybody watching could explain.
		private void Retarget(Transform target)
		{
			if (Target == target) return;

			Target = target;
			OnRetarget();
		}

		protected virtual void OnInitialize() { }
		protected virtual void OnEnter() { }
		protected virtual void OnExit() { }
		protected virtual void OnRetarget() => OnEnter();
	}
}
