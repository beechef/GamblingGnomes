using System;
using Unity.Netcode;

namespace Game.Runtime.GameMode.Poker.Items
{
	public enum PokerItemTableRuleKind : byte
	{
		NoFold = 0,
		ExtraStake = 1,

		// Only the rule's source may not fold on its street.
		NoFoldSelf = 2,

		// Nobody may fold in the all-in round that follows the rule's street.
		NoFoldAllIn = 3,

		// Only the rule's source may not fold, on any street or all-in round, until the hand ends. Its street
		// serial is only when it was laid.
		NoFoldSelfForHand = 4
	}

	// A rule an item put on the table for one street: nobody folds there, or every bet there stakes more.
	// Keyed by the street's serial (PokerItemModule.StreetSerial), never by its place in the hand, so a rule
	// can be aimed at the next street before it has opened.
	public struct PokerItemTableRule : INetworkSerializable, IEquatable<PokerItemTableRule>
	{
		public PokerItemTableRuleKind Kind;
		public int StreetSerial;
		public int Amount;
		public ulong SourceClientId;

		public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
		{
			serializer.SerializeValue(ref Kind);
			serializer.SerializeValue(ref StreetSerial);
			serializer.SerializeValue(ref Amount);
			serializer.SerializeValue(ref SourceClientId);
		}

		public bool Equals(PokerItemTableRule other) =>
			Kind == other.Kind && StreetSerial == other.StreetSerial && Amount == other.Amount && SourceClientId == other.SourceClientId;
	}
}
