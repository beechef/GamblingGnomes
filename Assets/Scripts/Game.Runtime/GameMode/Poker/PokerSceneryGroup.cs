namespace Game.Runtime.GameMode.Poker
{
	// The parts of the room anything outside the scene is allowed to ask for. An enum rather than a string
	// id because both halves have to name the same group and only one of them lives in the scene: a
	// dropdown on each side cannot be misspelled, where a pair of typed ids silently never match.
	public enum PokerSceneryGroup
	{
		Room,
		Table,
		Chairs,
		Props
	}
}
