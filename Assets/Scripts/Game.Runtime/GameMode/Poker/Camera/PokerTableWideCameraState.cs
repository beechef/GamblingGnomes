using Game.Runtime.Player.Camera;

namespace Game.Runtime.GameMode.Poker.Camera
{
	// The wide shot over the table. Alone among the shots here it does not turn the head — it genuinely
	// leaves the player's eyes — so it is a plain state holding a request on the scene's camera rather
	// than a look-at, and the look input is switched off for as long as it is up: the two beats that use
	// it are both waiting for the player to click something, and handing the look back takes the cursor.
	public class PokerTableWideCameraState : PlayerCameraState
	{
		protected override void OnEnter()
		{
			if (Controller && Controller.PlayerController)
				Controller.PlayerController.SetLookInputDisabled(true);

			if (PokerTableCamera.Instance) PokerTableCamera.Instance.Request(this);
		}

		protected override void OnExit()
		{
			if (PokerTableCamera.Instance) PokerTableCamera.Instance.Release(this);
		}
	}
}
