using Unity.Netcode;

namespace Game.Runtime.GameMode.Poker.Items
{
	// What the user pointed at, sent to the server as it is: the item, the target's seat, the target's card
	// (a hole slot, or a board slot for board items) and one of the user's own cards. Absent fields are -1.
	public struct PokerItemUseRequest : INetworkSerializable
	{
		private PokerItemType _item;
		private sbyte _targetSeat;
		private sbyte _cardSlot;
		private sbyte _ownSlot;

		public PokerItemType Item => _item;
		public int TargetSeat => _targetSeat;
		public int CardSlot => _cardSlot;
		public int OwnSlot => _ownSlot;

		public bool HasTarget => _targetSeat >= 0;
		public bool HasCard => _cardSlot >= 0;
		public bool HasOwnCard => _ownSlot >= 0;

		public PokerItemUseRequest(PokerItemType item, int targetSeat = -1, int cardSlot = -1, int ownSlot = -1)
		{
			_item = item;
			_targetSeat = Clamp(targetSeat);
			_cardSlot = Clamp(cardSlot);
			_ownSlot = Clamp(ownSlot);
		}

		public PokerItemUseRequest WithTarget(int seat) => new(Item, seat, CardSlot, OwnSlot);
		public PokerItemUseRequest WithCard(int slot) => new(Item, TargetSeat, slot, OwnSlot);
		public PokerItemUseRequest WithOwnCard(int slot) => new(Item, TargetSeat, CardSlot, slot);

		public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
		{
			serializer.SerializeValue(ref _item);
			serializer.SerializeValue(ref _targetSeat);
			serializer.SerializeValue(ref _cardSlot);
			serializer.SerializeValue(ref _ownSlot);
		}

		private static sbyte Clamp(int value) => value < 0 || value > sbyte.MaxValue ? (sbyte)-1 : (sbyte)value;
	}
}
