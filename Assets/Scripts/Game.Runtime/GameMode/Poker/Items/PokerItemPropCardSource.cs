namespace Game.Runtime.GameMode.Poker.Items
{
	// Where a card the item hands out flies from on every screen.
	public enum PokerItemPropCardSource : byte
	{
		Deck = 0,

		// The performance's own card point, placed in the player's frame: the vomit's card on the table.
		CardPoint = 1,

		// Under the table in front of the user (PokerSeat.ItemStashAnchor).
		Stash = 2
	}
}
