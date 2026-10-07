namespace Game.Runtime.GameMode.Poker.Items
{
	// How two cards changing places look in flight. Replicated in an RPC; never renumber.
	public enum PokerCardExchangeFlight : byte
	{
		// Each flies as this screen saw it.
		AsSeen = 0,

		// Both turn face down on the way, whatever this screen saw.
		FaceDown = 1,

		// The first turns face up for everybody (it is going public), the second flies face down.
		FirstFaceUp = 2
	}
}
