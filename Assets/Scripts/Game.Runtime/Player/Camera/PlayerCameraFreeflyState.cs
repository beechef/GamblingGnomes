namespace Game.Runtime.Player.Camera
{
	// The player's own eyes, turned by the player. It raises no camera of its own: they are already
	// looking through the one on whichever rig this client renders, and naming that camera here would
	// mean picking one of the two rigs — the owner renders the other one.
	//
	// So all it does is hand the look back, which is the whole difference between this state and every
	// other one.
	public class PlayerCameraFreeflyState : PlayerCameraState
	{
		protected override void OnEnter()
		{
			if (Controller && Controller.PlayerController) Controller.PlayerController.SetLookSuspended(false);
		}
	}
}
