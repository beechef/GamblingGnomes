namespace Game.Runtime.Player.Camera
{
	// The player's own eyes, turned by the player. It raises no camera of its own: they are already
	// looking through the one on whichever rig this client renders, and naming that camera here would
	// mean picking one of the two rigs — the owner renders the other one.
	//
	// So all it does is give the view back, which is the whole difference between this state and every
	// other one. Both halves are released, not because a camera state takes both — one only ever takes the
	// input — but because something else may have suspended the bone and gone away without handing it back,
	// and free flight is where the player is supposed to have their view again whatever happened before.
	public class PlayerCameraFreeflyState : PlayerCameraState
	{
		protected override void OnEnter()
		{
			if (!Controller || !Controller.PlayerController) return;

			Controller.PlayerController.SetLookSuspended(false);
			Controller.PlayerController.SetLookInputDisabled(false);
		}
	}
}
