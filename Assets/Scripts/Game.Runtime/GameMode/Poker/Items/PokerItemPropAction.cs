namespace Game.Runtime.GameMode.Poker.Items
{
	// What an item performance's action does to the prop or the performer.
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

		// Let go where it is: stays put in the world (a box set down on the table).
		Release = 4,

		// Travels to an anchor plus offset over Duration, following it, and rides it if it is on the body.
		MoveTo = 5,

		// Plays a state on the prop's animator.
		PlayState = 6,

		// Sends a method to the prop's components, for anything the prop does itself (its own effects).
		SendMessage = 7,

		// Plays a gesture on the performer.
		PlayGesture = 8,

		SpawnEffect = 9,
		PlaySound = 10,

		// Throws the die (PokerSuitDie) to land on the rewritten card's suit, once the rewrite has arrived.
		RollDie = 11,

		// The performance is over: the prop goes home, switched off.
		Finish = 12,

		// Aims the performer's hand IK at the player the item was used on (the knife pointed at them); the
		// gesture's IK curve decides when the arm follows (PlayerHandIkController).
		AimHandAtTarget = 13,

		// The card point stays where it is in the world while the prop goes on: a card carried there lies still
		// for the pick-up instead of riding a prop that shrinks away (the vomit landing on the table).
		DropCardPoint = 14
	}
}
