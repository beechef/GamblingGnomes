using System;
using Game.Runtime.GameMode.Poker.Items;
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

		// What kind of thing it is. PlainChip while the table plays money, and the seam for a table that
		// stakes something with a face on it.
		public PokerItemType ItemType;

		public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
		{
			serializer.SerializeValue(ref OwnerClientId);
			serializer.SerializeValue(ref Phase);
			serializer.SerializeValue(ref ItemType);
		}

		public bool Equals(PokerBetItem other) =>
			OwnerClientId == other.OwnerClientId && Phase == other.Phase && ItemType == other.ItemType;

		public override bool Equals(object obj) => obj is PokerBetItem other && Equals(other);

		public override int GetHashCode() => HashCode.Combine(OwnerClientId, (int)Phase, (int)ItemType);
	}
}
