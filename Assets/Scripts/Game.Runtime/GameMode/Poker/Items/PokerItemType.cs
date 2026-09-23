namespace Game.Runtime.GameMode.Poker.Items
{
	// Which item a card in somebody's hand is. It replicates, so a value is never renumbered and a retired
	// item keeps its number; the database row names its type rather than taking it from its position.
	public enum PokerItemType : byte
	{
		None = 0,
		PeekHand = 1,
		PeekBoard = 2,
		MutualReveal = 3,
		DeckCount = 4,
		SwapHand = 5,
		SwapBoard = 6,
		ExtraDraw = 7,
		HalfDose = 8,
		SharedRoll = 9,
		LockFold = 10,
		RaiseStakes = 11
	}
}
