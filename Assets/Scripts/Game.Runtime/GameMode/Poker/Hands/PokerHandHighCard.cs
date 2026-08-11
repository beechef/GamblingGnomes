using System.Collections.Generic;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Hands
{
	[CreateAssetMenu(fileName = "Hand_HighCard", menuName = "Game/Poker Hands/High Card")]
	public class PokerHandHighCard : PokerHandType
	{
		public override bool TryEvaluate(PokerCardAnalysis analysis, List<int> kickers)
		{
			if (analysis.CardCount == 0) return false;

			analysis.FillTopRanks(analysis.RankMask, 5, kickers);
			return true;
		}
	}
}
