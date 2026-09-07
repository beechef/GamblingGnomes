namespace Game.Runtime.GameMode.Poker
{
	public enum PokerActionType : byte
	{
		None = 0,
		Fold = 1,
		Check = 2,
		Call = 3,
		Raise = 4,
		AllIn = 5,
		Bet = 6,

		// The amount carries the kind of mushroom rather than a number of chips: a wager here is one cap
		// and never a sum, so there is nothing to raise and nothing to call.
		Wager = 7,

		// The amount carries a seat index rather than a size: naming somebody is the move, and who was
		// named is what the table is told.
		Target = 8
	}
}
