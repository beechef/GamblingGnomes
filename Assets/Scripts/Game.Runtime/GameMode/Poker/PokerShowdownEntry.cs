using System;
using Unity.Collections;
using Unity.Netcode;

namespace Game.Runtime.GameMode.Poker
{
	// One row of the showdown board. The cards are not carried here — they are already replicated on
	// each player and turned face up when the hand is shown, so the row only has to say who placed
	// where and with what.
	public struct PokerShowdownEntry : INetworkSerializable, IEquatable<PokerShowdownEntry>
	{
		public ulong ClientId;

		// 1-based, and shared by players who tied — two firsts are followed by a third, not a second.
		public int Rank;

		public FixedString32Bytes HandName;
		public int Winnings;

		public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
		{
			serializer.SerializeValue(ref ClientId);
			serializer.SerializeValue(ref Rank);
			serializer.SerializeValue(ref HandName);
			serializer.SerializeValue(ref Winnings);
		}

		public bool Equals(PokerShowdownEntry other) =>
			ClientId == other.ClientId && Rank == other.Rank && HandName.Equals(other.HandName) && Winnings == other.Winnings;

		public override bool Equals(object obj) => obj is PokerShowdownEntry other && Equals(other);
		public override int GetHashCode() => HashCode.Combine(ClientId, Rank, HandName, Winnings);
	}
}
