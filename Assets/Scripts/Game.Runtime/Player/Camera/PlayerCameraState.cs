using UnityEngine;

namespace Game.Runtime.Player.Camera
{
	// One thing the view can be doing, as a component on its own object. A state is not a row in a list
	// on the controller because states differ in *behaviour*, not in configuration: free flight hands the
	// look back to the player, and a shot turns the head onto whatever that shot is about. Adding one is
	// a new subclass and a new child object.
	//
	// A state resolves what it is looking at *itself* — it knows which beat it belongs to, so it can
	// listen for whose turn it is and swing on its own. Being handed a transform by whoever asked for the
	// state would put that knowledge in the caller, and every caller would carry a copy of it.
	//
	// Enter and Exit are not virtual. The controller owns entering and leaving — the invariants about
	// what is active live here, once — and a subclass fills in OnEnter/OnExit, which it cannot skip by
	// forgetting to call base.
	public abstract class PlayerCameraState : MonoBehaviour
	{
		public PlayerCameraController Controller { get; private set; }

		public bool IsActive { get; private set; }

		public void Initialize(PlayerCameraController controller)
		{
			Controller = controller;
			OnInitialize();
		}

		public void Enter()
		{
			if (IsActive) return;

			IsActive = true;
			OnEnter();
		}

		public void Exit()
		{
			if (!IsActive) return;

			IsActive = false;
			OnExit();
		}

		protected virtual void OnInitialize() { }
		protected virtual void OnEnter() { }
		protected virtual void OnExit() { }
	}
}
