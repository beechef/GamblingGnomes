namespace Game.Runtime.GameMode.Poker.Items
{
	// Where an item performance's action takes the prop, seen from the player performing it. Values are
	// serialized: never renumber.
	public enum PokerItemPropAnchor : byte
	{
		// Their right palm (PlayerBone.AttachRight), the joint the prop art is modelled against; a prop there is
		// turned with it.
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

		// Their root, at the chair: for offsets such as a toss over the shoulder.
		Body = 9,

		// Their left palm (PlayerBone.AttachLeft), turned with it like Hand.
		HandLeft = 10
	}
}
