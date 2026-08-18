namespace Game.Runtime.GameMode.Poker.Abilities
{
	// Where an accusation has got to. Replicated rather than inferred from whose turn it is: the UI has a
	// different question to ask in each of them, and reading a phase off a turn id is the kind of guess
	// that is right until somebody adds a fourth step.
	public enum PokerReportPhase : byte
	{
		None = 0,

		// The accuser is out of their chair with an arm across the table, looking for a face. Whoever
		// they settle on is lit up for the whole room.
		Aiming = 1,

		// A name has been said out loud and paid for in blood. The accused answers it or raises it, and a
		// raise hands the question back to the accuser.
		Response = 2,

		// Both of them are in and nobody is being asked anything. The table is left to sweat for a moment
		// before it finds out — a verdict that lands on the same frame as the last button press is a
		// verdict nobody watched arrive.
		Judging = 3,

		// Said and done, held on screen long enough to be read.
		Verdict = 4
	}
}
