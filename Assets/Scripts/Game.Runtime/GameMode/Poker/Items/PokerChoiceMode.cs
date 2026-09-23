namespace Game.Runtime.GameMode.Poker.Items
{
	// How one card inside an item is decided: pointed at by whoever is choosing, or drawn by the table.
	public enum PokerChoiceMode : byte
	{
		Chosen = 0,
		Random = 1
	}
}
