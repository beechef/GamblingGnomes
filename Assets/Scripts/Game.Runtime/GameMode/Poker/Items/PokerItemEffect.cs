using Game.Runtime.GameMode.Poker.Player;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Items
{
	// What one kind of mushroom does to whoever eats it — the ability shape, pointed at the loser.
	// Stateless: everything a bite touches lives on the table or the player, so one asset serves
	// every mushroom of its kind in the pot.
	public abstract class PokerItemEffect : ScriptableObject
	{
		public void ConsumeServer(PokerGameMode gameMode, PokerPlayer eater, byte itemType)
		{
			if (!gameMode || !eater || !eater.Data) return;

			OnConsumeServer(gameMode, eater, itemType);
		}

		protected abstract void OnConsumeServer(PokerGameMode gameMode, PokerPlayer eater, byte itemType);
	}
}
