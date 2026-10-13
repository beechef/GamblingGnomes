namespace Game.Runtime.GameMode.Poker.Items
{
	// When an item performance's action runs: a cue from the performer's clip, or a moment of the item itself.
	public enum PokerItemMoment : byte
	{
		// The item was just played (the gesture starts here).
		Played = 0,

		// The performer's clip raised a cue (PokerItemCues).
		Cue = 1,

		// The item has finished resolving: an answer, a card flight, a roll.
		Resolved = 2,

		// The item announced this outcome (a chance item: a nice smell or a foul one).
		Outcome = 3,

		// The fold lock the item laid on the performer has run out.
		FoldLockLifted = 4
	}
}
