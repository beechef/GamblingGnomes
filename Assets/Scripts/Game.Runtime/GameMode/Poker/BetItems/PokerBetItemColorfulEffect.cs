using Game.Runtime.GameMode.Poker.Player;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.BetItems
{
	// The cap nobody bets and everybody fears: the hand's winner picks who eats it. It costs the same
	// small amount whatever kinds the eater has met before — and then rolls against the total, so it can
	// end somebody outright rather than only bringing them closer to it.
	//
	// That roll is what makes naming a victim a decision worth making in public: feeding the player at
	// 70% is an attempt on their life, and feeding the one at 10% is a nudge.
	[CreateAssetMenu(fileName = "BetItemEffect_Colorful", menuName = "Game/Poker/Bet Item Effects/Colorful")]
	public class PokerBetItemColorfulEffect : PokerBetItemEffect
	{
		[Tooltip("Points added before the roll. Flat: this cap does not care which kinds the eater has already met.")]
		[MinValue(0)]
		[SerializeField] private int _gain = 10;

		[Tooltip("On, the roll is made against the rate *after* the gain, which is what the design document describes.")]
		[SerializeField] private bool _rollAfterGain = true;

		protected override void OnConsumeServer(PokerGameMode gameMode, PokerPlayer eater, PokerBetItemType itemType)
		{
			var data = eater.Data;

			var rateBefore = data.HallucinationRate.Value;
			data.ServerChangeHallucination(_gain);
			var rateAfter = data.HallucinationRate.Value;

			// Shown before it is paid: queued on the eater's roller, which the running beat starts with its own
			// pacing — every bar sweeps onto the number, and only then is a fatal roll's eater put under.
			if (eater.HallucinationRoll) eater.HallucinationRoll.ServerQueueRoll(_rollAfterGain ? rateAfter : rateBefore, rateBefore);
			else Debug.LogWarning($"[{name}] {eater.name} has no roller; the Colorful roll was skipped.", eater);
		}

		// The flat gain alone. The roll on top of it is chance, and one that fires is a player going under
		// rather than a player taking a hit — that has its own pose and no impact belongs in front of it.
		protected override int OnPreviewHallucinationGain(PokerGameMode gameMode, PokerPlayer eater, PokerBetItemType itemType) => _gain;
	}
}
