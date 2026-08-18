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

		// A name has been said out loud and paid for in blood. The accused answers it or raises it.
		Response = 2,

		// Said and done, held on screen long enough to be read.
		Verdict = 3
	}
}
