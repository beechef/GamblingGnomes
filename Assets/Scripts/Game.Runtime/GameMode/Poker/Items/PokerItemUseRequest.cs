namespace Game.Runtime.GameMode.Poker.Items
{
	// What the user pointed at, packed into the module command's one int: a byte each for the item, the
	// target's seat, the target's card (a hole slot, or a board slot for board items) and one of the user's
	// own cards. Absent fields are -1.
	public readonly struct PokerItemUseRequest
	{
		public PokerItemType Item { get; }
		public int TargetSeat { get; }
		public int CardSlot { get; }
		public int OwnSlot { get; }

		public bool HasTarget => TargetSeat >= 0;
		public bool HasCard => CardSlot >= 0;
		public bool HasOwnCard => OwnSlot >= 0;

		public PokerItemUseRequest(PokerItemType item, int targetSeat = -1, int cardSlot = -1, int ownSlot = -1)
		{
			Item = item;
			TargetSeat = targetSeat;
			CardSlot = cardSlot;
			OwnSlot = ownSlot;
		}

		public PokerItemUseRequest WithTarget(int seat) => new(Item, seat, CardSlot, OwnSlot);
		public PokerItemUseRequest WithCard(int slot) => new(Item, TargetSeat, slot, OwnSlot);
		public PokerItemUseRequest WithOwnCard(int slot) => new(Item, TargetSeat, CardSlot, slot);

		public int Pack() => (int)Item | Field(TargetSeat) << 8 | Field(CardSlot) << 16 | Field(OwnSlot) << 24;

		public static PokerItemUseRequest Unpack(int payload) => new(
			(PokerItemType)(payload & 0xFF),
			Unfield(payload >> 8),
			Unfield(payload >> 16),
			Unfield(payload >> 24));

		private static int Field(int value) => value < 0 ? 0 : (value + 1) & 0xFF;

		private static int Unfield(int packed) => (packed & 0xFF) - 1;
	}
}
