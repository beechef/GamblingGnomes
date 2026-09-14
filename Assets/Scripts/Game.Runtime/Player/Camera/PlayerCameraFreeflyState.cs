namespace Game.Runtime.Player.Camera
{
	// The player's own eyes, turned by the player. It raises no camera of its own: they are already looking
	// through the first-person one. So all it does is give the view back, which is the whole difference
	// between this state and every other one.
	public class PlayerCameraFreeflyState : PlayerCameraState
	{
		protected override void OnEnter()
		{
			if (!Controller || !Controller.PlayerController) return;

			Controller.PlayerController.SetLookInputDisabled(false);
		}
	}
}
