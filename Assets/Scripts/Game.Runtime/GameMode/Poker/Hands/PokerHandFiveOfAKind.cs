using System.Collections.Generic;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Hands
{
	// Only a deck with Jokers deals it: five of one value, ranked by the value.
	[CreateAssetMenu(fileName = "Hand_FiveOfAKind", menuName = "Game/Poker Hands/Five of a Kind")]
	public class PokerHandFiveOfAKind : PokerHandType
	{
		public override bool TryEvaluate(PokerCardAnalysis analysis, List<int> kickers)
		{
			var rank = analysis.HighestGroup(5);
			if (rank == 0) return false;

			kickers.Add(rank);
			return true;
		}
	}
}
