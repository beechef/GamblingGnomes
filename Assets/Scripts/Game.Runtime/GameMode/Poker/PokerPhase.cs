namespace Game.Runtime.GameMode.Poker
{
	// Stored as an integer in the stage assets (_phase, _foldPhase) and stamped on every pot entry, so the
	// numbers are fixed: retired values keep their gap rather than passing it on.
	public enum PokerPhase : byte
	{
		Waiting = 0,
		Dealing = 1,

		// The two moments a round asks for a cap: one before the cards are dealt and one after three of
		// them have been looked at. Separate values because the UI routes on the phase, and the second
		// wager is the only one that also offers folding.
		FirstWager = 8,
		SecondWager = 9,

		// Between them: everybody turns their own cards over at once, which is what the second wager is a
		// reaction to. Nobody holds a turn here — the table is waiting on all of them, not one of them.
		Looking = 10,

		// The consequence, taken one cap at a time. Its own phase because the UI has to be able to say the
		// hand is over and something is still happening.
		Eating = 11,

		Showdown = 6,
		Finished = 7,

		// One player is left in the running: the table announces them, blinks, and puts everything back.
		MatchOver = 12
	}
}
