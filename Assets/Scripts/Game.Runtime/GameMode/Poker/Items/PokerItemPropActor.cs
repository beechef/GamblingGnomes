namespace Game.Runtime.GameMode.Poker.Items
{
	// Who performs an item's prop: each one named gets a prop of their own.
	public enum PokerItemPropActor : byte
	{
		User = 0,
		Target = 1,
		UserAndTarget = 2
	}
}
