using System.Collections.Generic;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Hands
{
	[CreateAssetMenu(fileName = "Hand_Pair", menuName = "Game/Poker Hands/Pair")]
	public class PokerHandPair : PokerHandType
	{
		public override bool TryEvaluate(PokerCardAnalysis analysis, List<int> kickers)
		{
			var pair = analysis.HighestRankWithCount(2);
			if (pair == 0) return false;

			kickers.Add(pair);
			analysis.FillTopKickers(3, kickers, pair);
			return true;
		}
	}
}
