using System.Collections.Generic;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Hands
{
	[CreateAssetMenu(fileName = "Hand_TwoPair", menuName = "Game/Poker Hands/Two Pair")]
	public class PokerHandTwoPair : PokerHandType
	{
		public override bool TryEvaluate(PokerCardAnalysis analysis, List<int> kickers)
		{
			var highPair = analysis.HighestRankWithCount(2);
			if (highPair == 0) return false;

			var lowPair = analysis.HighestRankWithCount(2, highPair);
			if (lowPair == 0) return false;

			kickers.Add(highPair);
			kickers.Add(lowPair);
			analysis.FillTopKickers(1, kickers, highPair, lowPair);
			return true;
		}
	}
}
