namespace Game.Runtime.GameMode.Poker
{
	// Numbered explicitly and never renumbered: retired values keep their gap rather than passing it on.
	public enum PokerPlayerStatus : byte
	{
		Waiting = 0,
		Active = 1,
		Folded = 2,

		// Gone under. Read straight off the hallucination ceiling and lasts exactly as long as that does.
		Dead = 5
	}
}
