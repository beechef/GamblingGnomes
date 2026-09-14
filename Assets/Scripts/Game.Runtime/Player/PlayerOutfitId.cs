namespace Game.Runtime.Player
{
	// Names an outfit across every model, so asking for one means the same thing whatever body wears it —
	// a model that has no outfit by this name dresses in its own first one. Replicated by value, so a
	// value keeps its number for as long as it exists: add at the end and renumber nothing.
	public enum PlayerOutfitId
	{
		Classic
	}
}
