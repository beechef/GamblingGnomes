using System;
using Unity.Netcode;

namespace Game.Runtime.GameMode.Poker.Items
{
	public enum PokerItemTableRuleKind : byte
	{
		NoFold = 0,
		ExtraStake = 1,

		// Only the rule's source may not fold on its streets.
		NoFoldSelf = 2,

		// Nobody may fold in the all-in round that follows one of the rule's streets.
		NoFoldAllIn = 3,

		// Only the rule's source may not fold, on any street or all-in round, until the hand ends. Its street
		// serial is only when it was laid.
		NoFoldSelfForHand = 4
	}

	// A rule an item put on the table for a run of streets: nobody folds there, or every bet there stakes more.
	// Keyed by the first street's serial (PokerItemModule.StreetSerial), never by its place in the hand, so a
	// rule can be aimed at the next street before it has opened. It holds for Streets streets from there; the
	// hand ending clears it whatever is left.
	public struct PokerItemTableRule : INetworkSerializable, IEquatable<PokerItemTableRule>
	{
		public PokerItemTableRuleKind Kind;
		public int StreetSerial;
		public int Streets;
		public int Amount;
		public ulong SourceClientId;

		public bool Covers(int streetSerial) => streetSerial >= StreetSerial && streetSerial < StreetSerial + Math.Max(1, Streets);

		public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
		{
			serializer.SerializeValue(ref Kind);
			serializer.SerializeValue(ref StreetSerial);
			serializer.SerializeValue(ref Streets);
			serializer.SerializeValue(ref Amount);
			serializer.SerializeValue(ref SourceClientId);
		}

		public bool Equals(PokerItemTableRule other) =>
			Kind == other.Kind && StreetSerial == other.StreetSerial && Streets == other.Streets && Amount == other.Amount && SourceClientId == other.SourceClientId;
	}
}
