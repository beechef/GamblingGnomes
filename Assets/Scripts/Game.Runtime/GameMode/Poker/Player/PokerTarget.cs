namespace Game.Runtime.GameMode.Poker.Player
{
	// One thing pointed at: a player, a card in a player's hand, or a card on the board.
	public readonly struct PokerTarget
	{
		public PokerPlayer Player { get; }
		public int Slot { get; }
		public bool IsBoard { get; }

		public bool IsCard => Slot >= 0;

		public PokerTarget(PokerPlayer player, int slot, bool isBoard)
		{
			Player = player;
			Slot = slot;
			IsBoard = isBoard;
		}
	}
}
