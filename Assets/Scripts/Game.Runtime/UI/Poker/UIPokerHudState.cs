namespace Game.Runtime.UI.Poker
{
	// Which of the HUD's faces is up. Playing is every panel a hand is played with; Ranking is the
	// showdown board alone; Out is a player who has gone under, left with nothing until the waiting room.
	public enum UIPokerHudState
	{
		Playing,
		Ranking,
		Out
	}
}
