using System;
using Unity.Netcode;

namespace Game.Runtime.GameMode.Poker.Items
{
	// How many cards of each suit were still in the undealt deck when somebody counted. A snapshot: the
	// deck keeps moving, the count does not.
	public struct PokerSuitCounts : INetworkSerializable, IEquatable<PokerSuitCounts>
	{
		public bool IsKnown;
		public byte Clubs;
		public byte Diamonds;
		public byte Hearts;
		public byte Spades;

		public int Get(CardSuit suit) => suit switch
		{
			CardSuit.Clubs => Clubs,
			CardSuit.Diamonds => Diamonds,
			CardSuit.Hearts => Hearts,
			_ => Spades
		};

		public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
		{
			serializer.SerializeValue(ref IsKnown);
			serializer.SerializeValue(ref Clubs);
			serializer.SerializeValue(ref Diamonds);
			serializer.SerializeValue(ref Hearts);
			serializer.SerializeValue(ref Spades);
		}

		public bool Equals(PokerSuitCounts other) =>
			IsKnown == other.IsKnown && Clubs == other.Clubs && Diamonds == other.Diamonds && Hearts == other.Hearts && Spades == other.Spades;
	}
}
