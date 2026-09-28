namespace Game.Runtime.GameMode.Poker.Items
{
	// Whether an item may be used right now, asked by the picker and the server through the same call.
	// Neither can be played. The picker still draws a held item either way, dimmed with the reason beside
	// it; "hidden" is only the stronger statement that the rules forbid it here.
	public readonly struct PokerItemAvailability
	{
		public static readonly PokerItemAvailability Usable = new(true, null);

		public bool IsShown { get; }
		public string BlockReason { get; }

		public bool IsUsable => IsShown && string.IsNullOrEmpty(BlockReason);

		private PokerItemAvailability(bool isShown, string blockReason)
		{
			IsShown = isShown;
			BlockReason = blockReason;
		}

		public static PokerItemAvailability Hidden(string reason) => new(false, reason);

		public static PokerItemAvailability Dimmed(string reason) => new(true, reason);
	}
}
