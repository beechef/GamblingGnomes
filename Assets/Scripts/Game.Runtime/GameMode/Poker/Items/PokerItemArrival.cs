using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Items
{
	// How an item shows up in front of the player who has to eat it. An asset rather than a number on
	// the visual, because nobody has decided yet what the moment looks like — dropping onto the table,
	// flying across from whoever staked it, fading in — and each of those is a different animation, not
	// a different duration.
	//
	// The visual owns *where* the item ends up; this owns only how it gets there. That split is what
	// lets the layout be retuned without touching the arrival and the arrival be replaced without
	// touching the layout.
	public abstract class PokerItemArrival : ScriptableObject
	{
		[Tooltip("Seconds the arrival takes. The consume stage's bite is the budget it has to fit inside.")]
		[SerializeField] protected float _duration = 0.35f;

		public float Duration => Mathf.Max(0f, _duration);

		// The item is already parented to the plate and sitting at its resting place. Whatever this does
		// has to end there, because that is where the layout expects to find it.
		//
		// The source is where it came from — the winner's own place — or null when nothing handed it
		// over, which is what an arrival that does not travel ignores.
		public void Play(Transform item, Vector3 resting, Transform source)
		{
			if (!item) return;

			if (Duration <= 0f)
			{
				item.localPosition = resting;
				return;
			}

			OnPlay(item, resting, source);
		}

		protected abstract void OnPlay(Transform item, Vector3 resting, Transform source);
	}
}
