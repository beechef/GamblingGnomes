namespace Game.Runtime.GameMode.Poker
{
	// Numbered explicitly and never renumbered: retired values keep their gap rather than passing it on.
	public enum PokerActionType : byte
	{
		None = 0,
		Fold = 1,

		// The amount carries the kind of mushroom rather than a number of chips: a wager here is one cap
		// and never a sum, so there is nothing to raise and nothing to call.
		Wager = 7,

		// The amount carries a seat index rather than a size: naming somebody is the move, and who was
		// named is what the table is told.
		Target = 8
	}
}
