using Game.Runtime.GameMode.Poker.Player;

namespace Game.Runtime.GameMode.Poker.Items
{
	// Everything an item is handed when it is asked about or used: the table, the module running the
	// items, and who is holding the card.
	public readonly struct PokerItemContext
	{
		public PokerGameMode GameMode { get; }
		public PokerItemModule Module { get; }
		public PokerPlayer User { get; }

		public PokerGameData Data => GameMode ? GameMode.Data : null;

		public PokerItemContext(PokerGameMode gameMode, PokerItemModule module, PokerPlayer user)
		{
			GameMode = gameMode;
			Module = module;
			User = user;
		}
	}
}
