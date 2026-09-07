using Game.Runtime.GameMode.Poker.Player;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Mushrooms
{
	// The cap that sends you under. What it costs depends on whether the eater has met this kind
	// before, which is the whole reason the winner's choice of what to wager is worth making: feeding
	// somebody a kind they have never swallowed hurts twice as much as feeding them a familiar one.
	// The eater keeps the record, so the two numbers here are a price and nothing else.
	[CreateAssetMenu(fileName = "MushroomEffect_Hallucination", menuName = "Game/Poker/Mushroom Effects/Hallucination")]
	public class PokerMushroomHallucinationEffect : PokerMushroomEffect
	{
		[Tooltip("Points added when the eater has never swallowed this kind this match.")]
		[MinValue(0)]
		[SerializeField] private int _newTypeGain = 20;

		[Tooltip("Points added when they have. Smaller, or there would be nothing to choose between kinds.")]
		[MinValue(0)]
		[SerializeField] private int _repeatGain = 10;

		protected override void OnConsumeServer(PokerGameMode gameMode, PokerPlayer eater, byte itemType)
		{
			eater.Data.ServerEatMushroom(itemType, _newTypeGain, _repeatGain);
		}
	}
}
