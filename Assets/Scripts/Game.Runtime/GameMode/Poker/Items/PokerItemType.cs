namespace Game.Runtime.GameMode.Poker.Items
{
	// What a unit of stake is. Named here rather than left as a number, because the number travelled
	// through the pot ledger, the wallet, the wager action and every effect — and at none of those points
	// did anything say what a 4 was.
	//
	// The values are the ones the database was already handing out, so every ledger written before this
	// still means what it meant. **A value is what replicates, so it is fixed for the life of a save:**
	// renumbering one silently re-types every stake already staked, and a kind that is retired keeps its
	// number rather than letting the next one inherit it.
	public enum PokerItemType : byte
	{
		// A unit with no identity — what money is when the table is not playing with mushrooms, and what a
		// stake falls back to when no database is wired.
		PlainChip = 0,

		Green = 1,
		Red = 2,
		Purple = 3,
		Yellow = 4,

		// Never wagered and never dealt at random: the winner aims it at somebody.
		Colorful = 5
	}
}
