using System.Collections.Generic;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Hands
{
	[CreateAssetMenu(fileName = "Hand_TwoPair", menuName = "Game/Poker Hands/Two Pair")]
	public class PokerHandTwoPair : PokerHandType
	{
		public override bool TryEvaluate(PokerCardAnalysis analysis, List<int> kickers)
		{
			if (!analysis.TryTwoGroups(2, 2, out var highPair, out var lowPair)) return false;

			kickers.Add(highPair);
			kickers.Add(lowPair);
			analysis.FillTopKickers(1, kickers, highPair, lowPair);
			return true;
		}
	}
}
