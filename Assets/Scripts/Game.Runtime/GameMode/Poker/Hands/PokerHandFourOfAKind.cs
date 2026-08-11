using System.Collections.Generic;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Hands
{
	[CreateAssetMenu(fileName = "Hand_FourOfAKind", menuName = "Game/Poker Hands/Four of a Kind")]
	public class PokerHandFourOfAKind : PokerHandType
	{
		public override bool TryEvaluate(PokerCardAnalysis analysis, List<int> kickers)
		{
			var quad = analysis.HighestRankWithCount(4);
			if (quad == 0) return false;

			kickers.Add(quad);
			analysis.FillTopKickers(1, kickers, quad);
			return true;
		}
	}
}
