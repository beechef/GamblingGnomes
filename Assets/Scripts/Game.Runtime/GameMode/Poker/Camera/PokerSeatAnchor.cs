namespace Game.Runtime.GameMode.Poker.Camera
{
	// Which spot at a player's own chair a shot is about. The two are authored apart because the round
	// asks about each of them at a different moment: the first wager runs before the deal, so there is no
	// row of cards yet and what is being decided is the cap about to be put down.
	public enum PokerSeatAnchor
	{
		Card = 0,
		Item = 1
	}
}
