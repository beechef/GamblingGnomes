namespace Game.Runtime.GameMode.Poker.Camera
{
	// What a beat of the round is about, said in terms of what the player should be looking at rather than
	// which stage is running. A stage names one of these; the player prefab holds one state per name and
	// each of them owns what looking at that actually means.
	public enum PokerCameraShot
	{
		// Whatever the player was already looking at. The waiting room, and anything else that asks
		// nothing of them.
		None = 0,

		// The row of cards in front of this player's own chair.
		OwnCards = 1,

		// The spot in front of this chair where a staked cap lands. The first wager runs before the deal,
		// so there are no cards to frame yet and this is where the thing being decided will appear.
		OwnItems = 2,

		// Whoever the table is watching — the player on the clock, or the one swallowing a cap.
		FocusPlayer = 3,

		// The whole table at once, from a camera in the scene. The only shot here that leaves the
		// player's own eyes, and so the only one everybody sees identically.
		TableWide = 4
	}
}
