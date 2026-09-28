using System.Collections.Generic;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Hands
{
	[CreateAssetMenu(fileName = "Hand_FullHouse", menuName = "Game/Poker Hands/Full House")]
	public class PokerHandFullHouse : PokerHandType
	{
		public override bool TryEvaluate(PokerCardAnalysis analysis, List<int> kickers)
		{
			// Seven cards can hold two sets of trips, and the lower one plays as the pair.
			if (!analysis.TryTwoGroups(3, 2, out var trips, out var pair)) return false;

			kickers.Add(trips);
			kickers.Add(pair);
			return true;
		}
	}
}
