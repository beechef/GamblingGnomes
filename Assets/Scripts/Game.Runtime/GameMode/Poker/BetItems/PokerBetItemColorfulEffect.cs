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

			var against = _rollAfterGain ? rateAfter : rateBefore;
			if (against <= 0) return;

			// Already at the ceiling is not a roll to make: they are gone either way, and rolling would
			// only invite a reading where the highest rate somehow survives.
			if (!data.IsAlive) return;

			var roll = Random.Range(0, PokerPlayerData.MaxHallucination);
			var fatal = roll < against;

			// Shown before it is paid: queued on the eater's roller, which the running beat starts with its own
			// pacing — every bar sweeps onto the number, and only then is a fatal roll's eater put under. A body
			// with no roller still has to pay, so it pays at once.
			if (eater.HallucinationRoll)
			{
				eater.HallucinationRoll.ServerQueueRoll(roll, fatal, rateBefore, rateAfter);
				return;
			}

			if (fatal) data.ServerChangeHallucination(PokerPlayerData.MaxHallucination);
		}

		// The flat gain alone. The roll on top of it is chance, and one that fires is a player going under
		// rather than a player taking a hit — that has its own pose and no impact belongs in front of it.
		protected override int OnPreviewHallucinationGain(PokerGameMode gameMode, PokerPlayer eater, PokerBetItemType itemType) => _gain;
	}
}
