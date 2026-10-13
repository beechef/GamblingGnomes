namespace Game.Runtime.GameMode.Poker.Items
{
	// Where a card the item hands out flies from on every screen.
	public enum PokerItemPropCardSource : byte
	{
		Deck = 0,

		// A point on the user's prop (PokerItemPerformance's card point): a card carried down a vomit stream.
		Prop = 1,

		// Under the table in front of the user (PokerSeat.ItemStashAnchor).
		Stash = 2
	}
}
