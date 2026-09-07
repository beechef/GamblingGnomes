namespace Game.Runtime.GameMode.Poker
{
	public enum PokerPhase : byte
	{
		Waiting = 0,
		Dealing = 1,
		PreFlop = 2,
		Flop = 3,
		Turn = 4,
		River = 5,
		// The two moments a round asks for a cap: one before the cards are dealt and one after three of
		// them have been looked at. Separate values because the UI routes on the phase, and the second
		// wager is the only one that also offers folding.
		FirstWager = 8,
		SecondWager = 9,

		Showdown = 6,
		Finished = 7
	}
}
