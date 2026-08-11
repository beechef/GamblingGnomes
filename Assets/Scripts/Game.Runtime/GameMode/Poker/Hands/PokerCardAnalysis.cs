using System.Collections.Generic;

namespace Game.Runtime.GameMode.Poker.Hands
{
	// Counted once per hand and handed to every hand type, so adding a hand costs a class and not
	// another pass over the cards.
	public class PokerCardAnalysis
	{
		public readonly int[] RankCounts = new int[CardData.HighestRank + 1];
		public readonly int[] SuitCounts = new int[4];
		public readonly int[] SuitRankMasks = new int[4];

		public int RankMask { get; private set; }
		public int FlushSuit { get; private set; } = -1;
		public int CardCount { get; private set; }

		public void Fill(IReadOnlyList<CardData> cards)
		{
			for (var i = 0; i < RankCounts.Length; i++) RankCounts[i] = 0;
			for (var i = 0; i < SuitCounts.Length; i++)
			{
				SuitCounts[i] = 0;
				SuitRankMasks[i] = 0;
			}

			RankMask = 0;
			FlushSuit = -1;
			CardCount = 0;

			foreach (var card in cards)
			{
				if (!card.IsValid) continue;

				RankCounts[card.Rank]++;
				SuitCounts[card.Suit]++;
				SuitRankMasks[card.Suit] |= 1 << card.Rank;
				RankMask |= 1 << card.Rank;
				CardCount++;
			}

			for (var suit = 0; suit < 4; suit++)
			{
				if (SuitCounts[suit] >= 5) FlushSuit = suit;
			}
		}

		public int HighestRankWithCount(int count, int exclude = 0, int secondExclude = 0)
		{
			for (var rank = CardData.HighestRank; rank >= CardData.LowestRank; rank--)
			{
				if (rank == exclude || rank == secondExclude) continue;
				if (RankCounts[rank] >= count) return rank;
			}

			return 0;
		}

		public int StraightHigh(int rankMask)
		{
			for (var high = CardData.HighestRank; high >= 6; high--)
			{
				var window = 0;
				for (var offset = 0; offset < 5; offset++) window |= 1 << (high - offset);
				if ((rankMask & window) == window) return high;
			}

			// The wheel: the ace plays low and the hand is ranked by its five.
			const int wheel = (1 << 14) | (1 << 5) | (1 << 4) | (1 << 3) | (1 << 2);
			return (rankMask & wheel) == wheel ? 5 : 0;
		}

		public void FillTopRanks(int rankMask, int amount, List<int> buffer)
		{
			for (var rank = CardData.HighestRank; rank >= CardData.LowestRank && buffer.Count < amount; rank--)
			{
				if ((rankMask & (1 << rank)) != 0) buffer.Add(rank);
			}
		}

		public void FillTopKickers(int amount, List<int> buffer, int exclude = 0, int secondExclude = 0)
		{
			var wanted = buffer.Count + amount;

			for (var rank = CardData.HighestRank; rank >= CardData.LowestRank && buffer.Count < wanted; rank--)
			{
				if (RankCounts[rank] == 0) continue;
				if (rank == exclude || rank == secondExclude) continue;

				buffer.Add(rank);
			}
		}
	}
}
