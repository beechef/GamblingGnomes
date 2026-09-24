using System;
using Unity.Netcode;

namespace Game.Runtime.GameMode.Poker.Items
{
	// Where one card lies: a slot in somebody's hand, or a slot on the board when the holder is NoTurn.
	public struct PokerCardPlace : INetworkSerializable, IEquatable<PokerCardPlace>
	{
		public ulong HolderClientId;
		public int Slot;

		public bool IsBoard => HolderClientId == PokerGameData.NoTurn;

		public static PokerCardPlace InHand(ulong holderClientId, int slot) => new() { HolderClientId = holderClientId, Slot = slot };
		public static PokerCardPlace OnBoard(int slot) => new() { HolderClientId = PokerGameData.NoTurn, Slot = slot };

		public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
		{
			serializer.SerializeValue(ref HolderClientId);
			serializer.SerializeValue(ref Slot);
		}

		public bool Equals(PokerCardPlace other) => HolderClientId == other.HolderClientId && Slot == other.Slot;
	}
}
