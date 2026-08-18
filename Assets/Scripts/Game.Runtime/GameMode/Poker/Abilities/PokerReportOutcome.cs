namespace Game.Runtime.GameMode.Poker.Abilities
{
	// How an accusation ended. Three states, so it is one value rather than two bools that can disagree —
	// and only one of the three is allowed to say anything about the hand.
	public enum PokerReportOutcome : byte
	{
		// Nobody was named, or one of the two walked out. Nothing was staked in the end and nothing is said.
		Dropped = 0,

		// The accuser backed away from a shove. The blood on the table goes to the accused and the cards
		// stay face down — refusing to pay is refusing to find out, which is exactly what makes shoving
		// worth doing while guilty.
		Conceded = 1,

		// Both of them stood behind it, so the hand gets looked at. The only outcome where WasCheater
		// means anything.
		Judged = 2
	}
}
