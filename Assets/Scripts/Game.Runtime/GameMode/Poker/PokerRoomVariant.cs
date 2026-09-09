namespace Game.Runtime.GameMode.Poker
{
	// Which room the table is sitting in. Named on both sides — the asset asking for one and the scene
	// offering it — so the two cannot quietly fail to mean the same thing.
	//
	// A value is what an effect asset stores, so it is fixed once authored: renumbering one re-points every
	// effect already pointing at it.
	public enum PokerRoomVariant : byte
	{
		// The room as the scene built it. What everything falls back to when no effect is asking.
		Default = 0,

		Water = 1
	}
}
