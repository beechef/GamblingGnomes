using Game.Runtime.GameMode.Poker.Player;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Items
{
	// The card keeps its rank and takes one of the three other suits, drawn at random.
	[CreateAssetMenu(fileName = "PokerItem_RandomSuit", menuName = "Game/Poker/Items/Random Suit")]
	public class PokerItemRandomSuit : PokerItemRewriteCard
	{
		private const int SuitCount = 4;

		protected override bool TryRewrite(PokerPlayerData holder, int slot, out CardData card)
		{
			var old = holder.HoleCards[slot];
			var suit = (old.Suit + Random.Range(1, SuitCount)) % SuitCount;

			card = new CardData(old.Rank, (CardSuit)suit);
			return true;
		}
	}
}
