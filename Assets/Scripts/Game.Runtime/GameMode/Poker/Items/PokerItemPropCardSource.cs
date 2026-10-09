namespace Game.Runtime.GameMode.Poker.Items
{
	// Where a card the item hands out flies from on every screen.
	public enum PokerItemPropCardSource : byte
	{
		Deck = 0,

		// The user's prop itself: a card coughed up out of a puddle.
		Prop = 1,

		// Under the table in front of the user (PokerSeat.ItemStashAnchor).
		Stash = 2
	}
}
