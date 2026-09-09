using System;
using Unity.Netcode;

namespace Game.Runtime.GameMode.Poker.Items
{
	// One item, on the wire, in a list. It exists because `NetworkList<T>` wants `T : unmanaged,
	// IEquatable<T>` and a bare enum satisfies the first half only — the same reason CardData is a struct
	// rather than two loose bytes.
	//
	// The conversions are implicit in both directions, so nothing outside the list declarations has to
	// know it is here: a wallet is written and read as PokerItemType at every call site.
	public struct PokerItemUnit : INetworkSerializable, IEquatable<PokerItemUnit>
	{
		public PokerItemType Type;

		public static implicit operator PokerItemType(PokerItemUnit unit) => unit.Type;

		public static implicit operator PokerItemUnit(PokerItemType type) => new() { Type = type };

		public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
		{
			serializer.SerializeValue(ref Type);
		}

		public bool Equals(PokerItemUnit other) => Type == other.Type;

		public override bool Equals(object obj) => obj is PokerItemUnit other && Equals(other);

		public override int GetHashCode() => (int)Type;

		public override string ToString() => Type.ToString();
	}
}
