using System.Collections.Generic;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Hands
{
	[CreateAssetMenu(fileName = "Hand_FullHouse", menuName = "Game/Poker Hands/Full House")]
	public class PokerHandFullHouse : PokerHandType
	{
		public override bool TryEvaluate(PokerCardAnalysis analysis, List<int> kickers)
		{
			var trips = analysis.HighestRankWithCount(3);
			if (trips == 0) return false;

			// Seven cards can hold two sets of trips, and the lower one plays as the pair.
			var pair = analysis.HighestRankWithCount(2, trips);
			if (pair == 0) return false;

			kickers.Add(trips);
			kickers.Add(pair);
			return true;
		}
	}
}
