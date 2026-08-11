using System.Collections.Generic;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Hands
{
	[CreateAssetMenu(fileName = "Hand_Straight", menuName = "Game/Poker Hands/Straight")]
	public class PokerHandStraight : PokerHandType
	{
		public override bool TryEvaluate(PokerCardAnalysis analysis, List<int> kickers)
		{
			var high = analysis.StraightHigh(analysis.RankMask);
			if (high == 0) return false;

			kickers.Add(high);
			return true;
		}
	}
}
