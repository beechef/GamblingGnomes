using System;
using Unity.Netcode;

namespace Game.Runtime.GameMode.Poker.Items
{
	// One item on the wire, in a list: NetworkList wants IEquatable, which a bare enum is not.
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
