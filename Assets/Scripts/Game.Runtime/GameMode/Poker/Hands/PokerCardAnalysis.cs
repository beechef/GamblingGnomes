using System.Collections.Generic;

namespace Game.Runtime.GameMode.Poker.Hands
{
	// Counted once per hand and handed to every hand type, so adding a hand costs a class and not
	// another pass over the cards.
	//
	// Jokers are counted apart from ranks and suits and spent by the questions below, each asking what the
	// best card for the gap would be. With no Joker in the hand every answer is the plain count's.
	public class PokerCardAnalysis
	{
		private const int SuitCount = 4;

		public readonly int[] RankCounts = new int[CardData.HighestRank + 1];
		public readonly int[] SuitCounts = new int[SuitCount];
		public readonly int[] SuitRankMasks = new int[SuitCount];

		private readonly List<int> _flushCandidate = new();

		public int RankMask { get; private set; }
		public int CardCount { get; private set; }
		public int WildCount { get; private set; }

		public void Fill(IReadOnlyList<CardData> cards)
		{
			for (var i = 0; i < RankCounts.Length; i++) RankCounts[i] = 0;
			for (var i = 0; i < SuitCount; i++)
			{
				SuitCounts[i] = 0;
				SuitRankMasks[i] = 0;
			}

			RankMask = 0;
			CardCount = 0;
			WildCount = 0;

			foreach (var card in cards)
			{
				if (!card.IsValid) continue;

				CardCount++;

				if (card.IsJoker)
				{
					WildCount++;
					continue;
				}

				RankCounts[card.Rank]++;
				SuitCounts[card.Suit]++;
				SuitRankMasks[card.Suit] |= 1 << card.Rank;
				RankMask |= 1 << card.Rank;
			}
		}

		// The highest rank that makes `size` of a kind, Jokers filling what the hand lacks. 0 when none does.
		public int HighestGroup(int size, int exclude = 0) => HighestGroup(size, WildCount, exclude, out _);

		private int HighestGroup(int size, int wilds, int exclude, out int wildsUsed)
		{
			for (var rank = CardData.HighestRank; rank >= CardData.LowestRank; rank--)
			{
				if (rank == exclude) continue;

				var missing = size - RankCounts[rank];
				if (missing > wilds) continue;

				wildsUsed = missing > 0 ? missing : 0;
				return rank;
			}

			wildsUsed = 0;
			return 0;
		}

		// Two groups out of one supply of Jokers (a full house, two pair), the higher group ranked first.
		public bool TryTwoGroups(int firstSize, int secondSize, out int first, out int second)
		{
			for (var rank = CardData.HighestRank; rank >= CardData.LowestRank; rank--)
			{
				var missing = firstSize - RankCounts[rank];
				if (missing > WildCount) continue;

				var left = WildCount - (missing > 0 ? missing : 0);
				var other = HighestGroup(secondSize, left, rank, out _);
				if (other == 0) continue;

				first = rank;
				second = other;
				return true;
			}

			first = 0;
			second = 0;
			return false;
		}

		// Five ranks in a row out of the mask, Jokers filling the gaps. The ace also plays low.
		public int StraightHigh(int rankMask)
		{
			for (var high = CardData.HighestRank; high >= 6; high--)
			{
				if (Missing(rankMask, high - 4, high) <= WildCount) return high;
			}

			// The wheel: the ace plays low and the hand is ranked by its five.
			var wheelMissing = Missing(rankMask, 2, 5) + ((rankMask & (1 << CardData.HighestRank)) == 0 ? 1 : 0);
			return wheelMissing <= WildCount ? 5 : 0;
		}

		public int StraightFlushHigh()
		{
			var best = 0;
			for (var suit = 0; suit < SuitCount; suit++)
			{
				var high = StraightHigh(SuitRankMasks[suit]);
				if (high > best) best = high;
			}

			return best;
		}

		// The best five of one suit, highest first. A Joker plays as an ace of that suit, which may be a
		// second one: it stands for any card, one already held included.
		public bool TryFillFlush(List<int> buffer)
		{
			var found = false;
			var start = buffer.Count;

			for (var suit = 0; suit < SuitCount; suit++)
			{
				if (SuitCounts[suit] + WildCount < 5) continue;

				_flushCandidate.Clear();
				for (var i = 0; i < WildCount && _flushCandidate.Count < 5; i++) _flushCandidate.Add(CardData.HighestRank);
				FillTopRanks(SuitRankMasks[suit], 5, _flushCandidate);

				if (found && !Outranks(_flushCandidate, buffer, start)) continue;

				buffer.RemoveRange(start, buffer.Count - start);
				buffer.AddRange(_flushCandidate);
				found = true;
			}

			return found;
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

		private static int Missing(int rankMask, int low, int high)
		{
			var missing = 0;
			for (var rank = low; rank <= high; rank++)
			{
				if ((rankMask & (1 << rank)) == 0) missing++;
			}

			return missing;
		}

		private static bool Outranks(List<int> candidate, List<int> current, int start)
		{
			for (var i = 0; i < candidate.Count && start + i < current.Count; i++)
			{
				if (candidate[i] != current[start + i]) return candidate[i] > current[start + i];
			}

			return false;
		}
	}
}
