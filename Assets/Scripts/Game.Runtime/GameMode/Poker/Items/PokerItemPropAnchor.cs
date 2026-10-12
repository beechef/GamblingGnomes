namespace Game.Runtime.GameMode.Poker.Items
{
	// Where one step of an item's prop performance takes the prop, seen from the player performing it.
	public enum PokerItemPropAnchor : byte
	{
		// Their right hand's hold point; the prop rides it until a later step takes it away.
		Hand = 0,

		// Under the table edge in front of their chair (PokerSeat.ItemStashAnchor).
		Stash = 1,

		// On the table in front of them (PokerSeat.CardAnchor).
		Table = 2,

		Mouth = 3,
		Nose = 4,

		// The hand holding their cards (PokerHandVisual.HandAnchor).
		Cards = 5,

		// Floating before them: their focus point, or in front of your own eyes when it is you.
		Focus = 6,

		// The other party to the item: the target for the user, the user for the target.
		Other = 7,

		// The last board card still face down.
		BoardCard = 8,

		// Their root, at the chair: for offsets such as a toss over the shoulder.
		Body = 9
	}
}
