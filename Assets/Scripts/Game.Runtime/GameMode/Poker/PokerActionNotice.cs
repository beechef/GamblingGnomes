using System;
using Unity.Netcode;

namespace Game.Runtime.GameMode.Poker
{
	// One accepted table action, announced to every seat: who, what, and what it cost them. It rides a
	// sequence number so the same player calling twice in a row still reads as two announcements.
	public struct PokerActionNotice : INetworkSerializable, IEquatable<PokerActionNotice>
	{
		public ulong ClientId;
		public PokerActionType Action;
		public int Amount;
		public int Sequence;

		public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
		{
			serializer.SerializeValue(ref ClientId);
			serializer.SerializeValue(ref Action);
			serializer.SerializeValue(ref Amount);
			serializer.SerializeValue(ref Sequence);
		}

		public bool Equals(PokerActionNotice other) =>
			ClientId == other.ClientId && Action == other.Action && Amount == other.Amount && Sequence == other.Sequence;
	}
}
