using System.Collections.Generic;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Hands
{
	[CreateAssetMenu(fileName = "Hand_ThreeOfAKind", menuName = "Game/Poker Hands/Three of a Kind")]
	public class PokerHandThreeOfAKind : PokerHandType
	{
		public override bool TryEvaluate(PokerCardAnalysis analysis, List<int> kickers)
		{
			var trips = analysis.HighestRankWithCount(3);
			if (trips == 0) return false;

			kickers.Add(trips);
			analysis.FillTopKickers(2, kickers, trips);
			return true;
		}
	}
}
