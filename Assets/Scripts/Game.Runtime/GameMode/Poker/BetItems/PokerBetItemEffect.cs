using Game.Runtime.GameMode.Poker.Player;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.BetItems
{
	// What one kind of mushroom does to whoever eats it — the ability shape, pointed at the loser.
	// Stateless: everything a bite touches lives on the table or the player, so one asset serves
	// every mushroom of its kind in the pot.
	public abstract class PokerBetItemEffect : ScriptableObject
	{
		public void ConsumeServer(PokerGameMode gameMode, PokerPlayer eater, PokerBetItemType itemType)
		{
			if (!gameMode || !eater || !eater.Data) return;

			OnConsumeServer(gameMode, eater, itemType);
		}

		// What this bite is about to add, asked before it is taken. Not a prediction: the only thing the
		// price turns on is a record that nothing has written yet, so the answer is exact — which is what
		// lets the eating beat know it owes an impact before the rate moves, and so play the hit before the
		// world changes rather than on top of it.
		//
		// Anything decided by chance is outside this on purpose. A Colorful cap that rolls somebody under
		// goes to the ceiling, and that is a death rather than a hit: it has its own pose and wants no
		// impact in front of it.
		public int PreviewHallucinationGain(PokerGameMode gameMode, PokerPlayer eater, PokerBetItemType itemType)
		{
			if (!gameMode || !eater || !eater.Data) return 0;

			return OnPreviewHallucinationGain(gameMode, eater, itemType);
		}

		protected abstract void OnConsumeServer(PokerGameMode gameMode, PokerPlayer eater, PokerBetItemType itemType);

		// Zero for a kind that costs no hallucination at all, which is the honest answer for most of them.
		protected virtual int OnPreviewHallucinationGain(PokerGameMode gameMode, PokerPlayer eater, PokerBetItemType itemType) => 0;
	}
}
