namespace Game.Runtime.GameMode.Poker
{
	public enum PokerPlayerStatus : byte
	{
		Waiting = 0,
		Active = 1,
		Folded = 2,
		AllIn = 3,
		Busted = 4
	}
}
