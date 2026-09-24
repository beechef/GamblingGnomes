namespace Game.Runtime.GameMode.Poker.Items
{
	// Whether an item may be used right now, asked by the picker and the server through the same call.
	// What the rules forbid is hidden; what merely cannot be done yet is dimmed with the reason beside it.
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
