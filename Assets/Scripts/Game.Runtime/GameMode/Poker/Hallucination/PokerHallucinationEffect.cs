using Game.Runtime.GameMode.Poker.Player;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Hallucination
{
	// What one level of hallucination does to the world a player sees. Client-side and per-viewer: this
	// is the only thing in the round that is *not* the same on every screen, and it must not be — a
	// player at 80% and one at 10% are meant to be describing different rooms to each other.
	//
	// This half is config and nothing else. The asset is shared by every rung pointing at it and holds no
	// runtime state at all, so the same effect sitting in two pools cannot have one of them tear down what
	// the other believes it owns. Running it spawns an object that does the work, which is also what makes
	// a hallucination something you can see in the hierarchy and retune while it is on screen.
	public abstract class PokerHallucinationEffect : ScriptableObject
	{
		public PokerHallucinationEffectBehaviour Run(Transform parent, PokerPlayer viewer)
		{
			if (!viewer) return null;

			var host = new GameObject(name);
			host.transform.SetParent(parent, false);

			var behaviour = Attach(host);
			behaviour.Begin(this, viewer);

			return behaviour;
		}

		protected abstract PokerHallucinationEffectBehaviour Attach(GameObject host);
	}
}
