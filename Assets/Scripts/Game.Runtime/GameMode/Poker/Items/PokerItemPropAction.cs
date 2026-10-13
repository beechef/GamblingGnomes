namespace Game.Runtime.GameMode.Poker.Items
{
	// What an item performance's action does to the prop or the performer. Serialized: never renumber; a
	// retired value's number is never reused.
	public enum PokerItemPropAction : byte
	{
		// Switches the prop on where it is, growing it in over Duration.
		Show = 0,

		// Shrinks the prop away over Duration and switches it off.
		Hide = 1,

		// Back on the bone it was authored on, at its authored pose (cuffs back on the right hand).
		ReturnHome = 2,

		// Rides an anchor from exactly where it is in the world (cuffs left on the left wrist).
		AttachTo = 3,

		// Travels to an anchor plus offset over Duration, following it, and rides it if it is on the body.
		MoveTo = 5,

		// Plays a state on the prop's animator.
		PlayState = 6,

		// Plays a gesture on the performer.
		PlayGesture = 8,

		SpawnEffect = 9,

		// Throws the die (PokerSuitDie) to land on the rewritten card's suit, once the rewrite has arrived.
		RollDie = 11,

		// The performance is over: the prop goes home, switched off.
		Finish = 12,

		// Aims the performer's hand IK at the player the item was used on (the knife pointed at them); the
		// gesture's IK curve decides when the arm follows (PlayerHandIkController).
		AimHandAtTarget = 13,

		// The card point rides an anchor from where it is, leaving the prop: a card carried there lies still on
		// the table while the prop shrinks away (Table), then goes with the hand that picks it up (Hand).
		AttachCardPoint = 14,

		// Aims the performer's hand IK at an anchor (the card point it reaches for); the gesture's IK curve
		// decides when the arm follows (PlayerHandIkController).
		AimHand = 15
	}
}
