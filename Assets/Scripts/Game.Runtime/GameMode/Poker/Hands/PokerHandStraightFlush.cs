using System.Collections.Generic;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Hands
{
	[CreateAssetMenu(fileName = "Hand_StraightFlush", menuName = "Game/Poker Hands/Straight Flush")]
	public class PokerHandStraightFlush : PokerHandType
	{
		public override bool TryEvaluate(PokerCardAnalysis analysis, List<int> kickers)
		{
			var high = analysis.StraightFlushHigh();
			if (high == 0) return false;

			kickers.Add(high);
			return true;
		}
	}
}
