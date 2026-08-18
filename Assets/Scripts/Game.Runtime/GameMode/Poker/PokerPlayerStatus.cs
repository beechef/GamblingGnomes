namespace Game.Runtime.GameMode.Poker
{
	public enum PokerPlayerStatus : byte
	{
		Waiting = 0,
		Active = 1,
		Folded = 2,
		AllIn = 3,
		Busted = 4,

		// Out of blood. Unlike Busted, which a hand can put right, this one is read straight off a health
		// of zero and lasts exactly as long as that does.
		Dead = 5
	}
}
