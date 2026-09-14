using System;
using System.Collections.Generic;
using Game.Runtime.GameMode.Poker.Player;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Hallucination
{
	// What an effect acts on, split out from the effect itself. Making a thing bigger, painting it and
	// hanging something off it all have to answer the same question first, and the answer is the half a
	// designer actually changes: one scale effect aimed at a head or at a whole body is two different
	// hallucinations.
	//
	// A resolver and nothing else. It holds no state, because a target asset is shared by every effect
	// pointing at it while the effects themselves are cloned per rung.
	public abstract class PokerHallucinationTarget : ScriptableObject
	{
		public void Collect(PokerPlayer viewer, List<Transform> into)
		{
			if (!viewer || into == null) return;

			into.Clear();
			OnCollect(viewer, into);
		}

		// What a target points at can appear after an effect has begun: a hand is dealt again every round
		// and a player can sit down mid-match, so an effect that resolved once would quietly stop covering
		// anything new. The target owns which event says its set moved; the effect owns what to do about it.
		public virtual void Subscribe(PokerPlayer viewer, Action onChanged) { }

		public virtual void Unsubscribe(PokerPlayer viewer, Action onChanged) { }

		// Whether what this hands over is a player's body, as opposed to something that merely hangs under
		// one. Cards and caps are children of the player they belong to, so asking the hierarchy finds the
		// body above a card just as surely as above a head — and a card effect then repaints the player.
		public virtual bool ResolvesBodies => false;

		protected abstract void OnCollect(PokerPlayer viewer, List<Transform> into);
	}
}
