namespace Game.Runtime.GameMode.Poker
{
	// Which shot of the match's survivor every screen is watching. Stored as a byte on the wire, so values
	// are never renumbered.
	public enum PokerVictoryShot : byte
	{
		None = 0,

		// The whole survivor dancing on the table.
		Wide = 1,

		// Face to face with the survivor while the table names them.
		CloseUp = 2
	}
}
