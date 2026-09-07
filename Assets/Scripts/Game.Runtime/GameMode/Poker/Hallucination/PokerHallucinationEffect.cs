using Game.Runtime.GameMode.Poker.Player;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Hallucination
{
	// What one level of hallucination does to the world a player sees. Client-side and per-viewer: this
	// is the only thing in the round that is *not* the same on every screen, and it must not be — a
	// player at 80% and one at 10% are meant to be describing different rooms to each other.
	//
	// Begin and End rather than a single "apply the state": effects stack, so several are running at
	// once and each has to be able to take itself down without disturbing the others.
	public abstract class PokerHallucinationEffect : ScriptableObject
	{
		public void Begin(PokerPlayer viewer)
		{
			if (!viewer) return;

			OnBegin(viewer);
		}

		public void End(PokerPlayer viewer)
		{
			if (!viewer) return;

			OnEnd(viewer);
		}

		protected abstract void OnBegin(PokerPlayer viewer);
		protected abstract void OnEnd(PokerPlayer viewer);
	}
}
