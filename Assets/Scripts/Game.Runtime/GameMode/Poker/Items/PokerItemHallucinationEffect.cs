using Game.Runtime.GameMode.Poker.Player;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Items
{
	// The cap that sends you under. What it costs depends on whether the eater has met this kind
	// before, which is the whole reason the winner's choice of what to wager is worth making: feeding
	// somebody a kind they have never swallowed hurts twice as much as feeding them a familiar one.
	//
	// Both the price and the decision live here. The eater's controller keeps the record and answers
	// whether a kind is new — that is a fact about them — but which of these two numbers that buys is
	// this cap's own business, and another kind is free to charge for the same record differently or
	// ignore it entirely.
	[CreateAssetMenu(fileName = "ItemEffect_Hallucination", menuName = "Game/Poker/Item Effects/Hallucination")]
	public class PokerItemHallucinationEffect : PokerItemEffect
	{
		[Tooltip("Points added when the eater has never swallowed this kind this match.")]
		[MinValue(0)]
		[SerializeField] private int _newTypeGain = 20;

		[Tooltip("Points added when they have. Smaller, or there would be nothing to choose between kinds.")]
		[MinValue(0)]
		[SerializeField] private int _repeatGain = 10;

		protected override void OnConsumeServer(PokerGameMode gameMode, PokerPlayer eater, PokerItemType itemType)
		{
			var consume = eater.ItemConsume;

			// Read before the record is written, or every mouthful reads as one they have had before.
			var metBefore = consume && consume.HasConsumed(itemType);

			if (consume) consume.ServerRecordConsumed(itemType);

			eater.Data.ServerChangeHallucination(metBefore ? _repeatGain : _newTypeGain);
		}
	}
}
