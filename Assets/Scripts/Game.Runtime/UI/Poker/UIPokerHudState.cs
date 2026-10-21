namespace Game.Runtime.UI.Poker
{
	// Which of the HUD's faces is up. Playing is every panel a hand is played with; Ranking is the
	// showdown board alone; Out is a player who has gone under, left with nothing until the waiting room;
	// Victory is the match's survivor on show, the screen left to the shot and the name board.
	public enum UIPokerHudState
	{
		Playing,
		Ranking,
		Out,
		Victory
	}
}
