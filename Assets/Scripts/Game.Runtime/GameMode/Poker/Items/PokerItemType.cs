namespace Game.Runtime.GameMode.Poker.Items
{
	// Which item a card in somebody's hand is. It replicates, so a value is never renumbered and a retired
	// item's number is never reused; the database row names its type rather than taking it from its position.
	public enum PokerItemType : byte
	{
		None = 0,
		PeekBoard = 2,
		SwapHand = 5,
		ExtraDraw = 7,
		HalfDose = 8,
		SharedRoll = 9,
		PlaceOnBoard = 12,
		RandomSuit = 13,
		PairLockFold = 17,
		RateShift = 18,
		TableReveal = 19,
		JokerCard = 20
	}
}
