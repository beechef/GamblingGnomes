namespace Game.Runtime.GameMode.Poker
{
	public enum PokerActionType : byte
	{
		None = 0,
		Fold = 1,
		Check = 2,
		Call = 3,
		Raise = 4,
		AllIn = 5,
		Bet = 6
	}
}
