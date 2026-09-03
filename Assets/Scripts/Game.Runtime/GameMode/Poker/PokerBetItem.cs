using System;
using Unity.Netcode;

namespace Game.Runtime.GameMode.Poker
{
	// One unit of stake in the pot: who fed it, on which street, and what kind of thing it is. The pot
	// scalar stays what every rule computes with — this is its itemised twin, written only by
	// PokerTableUtility beside the scalar so the two cannot disagree.
	public struct PokerBetItem : INetworkSerializable, IEquatable<PokerBetItem>
	{
		public ulong OwnerClientId;
		public PokerPhase Phase;

		// Index into whatever catalogue gives units an identity of their own — zero while the table
		// plays plain chips, the seam for a table that stakes something with a face on it.
		public byte ItemTypeIndex;

		public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
		{
			serializer.SerializeValue(ref OwnerClientId);
			serializer.SerializeValue(ref Phase);
			serializer.SerializeValue(ref ItemTypeIndex);
		}

		public bool Equals(PokerBetItem other) =>
			OwnerClientId == other.OwnerClientId && Phase == other.Phase && ItemTypeIndex == other.ItemTypeIndex;

		public override bool Equals(object obj) => obj is PokerBetItem other && Equals(other);

		public override int GetHashCode() => HashCode.Combine(OwnerClientId, (int)Phase, ItemTypeIndex);
	}
}
