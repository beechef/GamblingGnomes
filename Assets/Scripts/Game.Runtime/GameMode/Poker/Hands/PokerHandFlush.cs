using System.Collections.Generic;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Hands
{
	[CreateAssetMenu(fileName = "Hand_Flush", menuName = "Game/Poker Hands/Flush")]
	public class PokerHandFlush : PokerHandType
	{
		public override bool TryEvaluate(PokerCardAnalysis analysis, List<int> kickers)
		{
			if (analysis.FlushSuit < 0) return false;

			analysis.FillTopRanks(analysis.SuitRankMasks[analysis.FlushSuit], 5, kickers);
			return true;
		}
	}
}
