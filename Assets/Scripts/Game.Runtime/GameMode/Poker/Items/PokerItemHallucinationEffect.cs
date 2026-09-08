using Game.Runtime.GameMode.Poker.Player;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Items
{
	// The cap that sends you under. What it costs depends on whether the eater has met this kind
	// before, which is the whole reason the winner's choice of what to wager is worth making: feeding
	// somebody a kind they have never swallowed hurts twice as much as feeding them a familiar one.
	// The eater keeps the record, so the two numbers here are a price and nothing else.
	[CreateAssetMenu(fileName = "ItemEffect_Hallucination", menuName = "Game/Poker/Item Effects/Hallucination")]
	public class PokerItemHallucinationEffect : PokerItemEffect
	{
		[Tooltip("Points added when the eater has never swallowed this kind this match.")]
		[MinValue(0)]
		[SerializeField] private int _newTypeGain = 20;

		[Tooltip("Points added when they have. Smaller, or there would be nothing to choose between kinds.")]
		[MinValue(0)]
		[SerializeField] private int _repeatGain = 10;

		protected override void OnConsumeServer(PokerGameMode gameMode, PokerPlayer eater, byte itemType)
		{
			if (eater.Items) eater.Items.ServerConsume(itemType, _newTypeGain, _repeatGain);
		}
	}
}
