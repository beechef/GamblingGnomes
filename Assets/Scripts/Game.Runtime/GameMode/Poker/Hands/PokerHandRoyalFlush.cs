using System.Collections.Generic;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Hands
{
	// A straight flush that runs up to the ace. It needs no kickers: two royal flushes can only tie.
	[CreateAssetMenu(fileName = "Hand_RoyalFlush", menuName = "Game/Poker Hands/Royal Flush")]
	public class PokerHandRoyalFlush : PokerHandType
	{
		public override bool TryEvaluate(PokerCardAnalysis analysis, List<int> kickers)
		{
			return analysis.StraightFlushHigh() == CardData.HighestRank;
		}
	}
}
