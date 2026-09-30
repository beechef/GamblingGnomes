using System.Collections.Generic;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Hands
{
	[CreateAssetMenu(fileName = "Hand_Flush", menuName = "Game/Poker Hands/Flush")]
	public class PokerHandFlush : PokerHandType
	{
		public override bool TryEvaluate(PokerCardAnalysis analysis, List<int> kickers)
		{
			return analysis.TryFillFlush(kickers);
		}
	}
}
