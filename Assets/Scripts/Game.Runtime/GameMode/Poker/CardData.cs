using System;
using Unity.Netcode;

namespace Game.Runtime.GameMode.Poker
{
	public struct CardData : INetworkSerializable, IEquatable<CardData>
	{
		public byte Rank;
		public byte Suit;

		public const byte LowestRank = 2;
		public const byte HighestRank = 14;

		// A wild card: any rank and any suit. It carries no suit of its own, so nothing counting by suit
		// may count it.
		public const byte JokerRank = 15;

		public static CardData None => new() { Rank = 0, Suit = 0 };
		public static CardData Joker => new() { Rank = JokerRank, Suit = 0 };

		public bool IsJoker => Rank == JokerRank;
		public bool IsValid => IsJoker || (Rank >= LowestRank && Rank <= HighestRank);
		public CardSuit SuitType => (CardSuit)Suit;

		// Suit-major so a 52 entry sprite list can be indexed straight off a card. A Joker has no place in it.
		public int DatabaseIndex => Suit * 13 + (Rank - LowestRank);

		public CardData(byte rank, CardSuit suit)
		{
			Rank = rank;
			Suit = (byte)suit;
		}

		public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
		{
			serializer.SerializeValue(ref Rank);
			serializer.SerializeValue(ref Suit);
		}

		public bool Equals(CardData other) => Rank == other.Rank && Suit == other.Suit;
		public override bool Equals(object obj) => obj is CardData other && Equals(other);
		public override int GetHashCode() => Rank * 4 + Suit;

		public override string ToString()
		{
			if (!IsValid) return "--";
			if (IsJoker) return "JOKER";

			var rank = Rank switch
			{
				11 => "J",
				12 => "Q",
				13 => "K",
				14 => "A",
				_ => Rank.ToString()
			};

			var suit = SuitType switch
			{
				CardSuit.Clubs => "♣",
				CardSuit.Diamonds => "♦",
				CardSuit.Hearts => "♥",
				_ => "♠"
			};

			return rank + suit;
		}
	}
}
