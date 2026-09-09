using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Hallucination
{
	public class PokerHallucinationRoomBehaviour : PokerHallucinationEffectBehaviour<PokerHallucinationRoomEffect>
	{
		// The swap out is worth watching, so the host outlives the moment it was taken off by however long
		// the room says it takes.
		protected override float LingerSeconds =>
			PokerRoomController.Instance ? PokerRoomController.Instance.TransitionDuration : 0f;

		protected override void OnBegin()
		{
			// Gameplay is loaded additively and a client can be part-way through it, so the room can arrive
			// after this began — the same reason every target here carries a change event.
			PokerRoomController.OnInstanceChanged += Reapply;

			Reapply();
		}

		protected override void OnEnd()
		{
			PokerRoomController.OnInstanceChanged -= Reapply;

			if (PokerRoomController.Instance) PokerRoomController.Instance.Clear(this);
		}

		private void Reapply()
		{
			var controller = PokerRoomController.Instance;
			if (!controller) return;

			controller.Set(this, Config.Room);
		}
	}
}
