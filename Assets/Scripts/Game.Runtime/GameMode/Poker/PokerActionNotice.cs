using System;
using Unity.Netcode;

namespace Game.Runtime.GameMode.Poker
{
	// One accepted table action, announced to every seat: who, and what. It rides a sequence number so the
	// same player acting twice in a row still reads as two announcements.
	public struct PokerActionNotice : INetworkSerializable, IEquatable<PokerActionNotice>
	{
		public ulong ClientId;
		public PokerActionType Action;
		public int Sequence;

		public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
		{
			serializer.SerializeValue(ref ClientId);
			serializer.SerializeValue(ref Action);
			serializer.SerializeValue(ref Sequence);
		}

		public bool Equals(PokerActionNotice other) =>
			ClientId == other.ClientId && Action == other.Action && Sequence == other.Sequence;
	}
}
