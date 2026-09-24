namespace Game.Runtime.GameMode
{
	// Stored as an int in GameModeDatabase and Bootstrap's lobby settings, so a value is never renumbered.
	public enum GameModeType
	{
		Poker = 1,
		PokerLiar = 2,
		PokerIndian = 3,
		PokerIndianNoBoard = 4
	}
}
