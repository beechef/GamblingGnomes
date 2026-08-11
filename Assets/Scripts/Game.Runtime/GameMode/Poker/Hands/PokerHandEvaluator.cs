using System.Collections.Generic;

namespace Game.Runtime.GameMode.Poker.Hands
{
	// Walks the database from the highest tier down and takes the first hand that matches, so which
	// hands exist and how they outrank each other is entirely the database's business.
	public class PokerHandEvaluator
	{
		private const int RankBase = 15;
		private const int MaxKickers = 5;

		private readonly PokerCardAnalysis _analysis = new();
		private readonly List<int> _kickers = new();

		public PokerHandResult Evaluate(PokerHandDatabase database, IReadOnlyList<CardData> cards)
		{
			if (!database || cards == null || cards.Count == 0) return PokerHandResult.None;

			_analysis.Fill(cards);

			foreach (var handType in database.HandTypes)
			{
				if (!handType) continue;

				_kickers.Clear();
				if (!handType.TryEvaluate(_analysis, _kickers)) continue;

				return new PokerHandResult(handType, PackScore(handType.Tier, _kickers));
			}

			return PokerHandResult.None;
		}

		private static long PackScore(int tier, List<int> kickers)
		{
			long score = tier;

			for (var i = 0; i < MaxKickers; i++)
			{
				score = score * RankBase + (i < kickers.Count ? kickers[i] : 0);
			}

			return score;
		}
	}
}
