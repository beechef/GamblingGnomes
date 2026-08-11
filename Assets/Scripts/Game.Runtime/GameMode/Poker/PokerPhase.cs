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
		Showdown = 6,
		Finished = 7
	}
}
