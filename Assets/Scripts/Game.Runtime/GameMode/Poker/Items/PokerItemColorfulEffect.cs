using Game.Runtime.GameMode.Poker.Player;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Items
{
	// The cap nobody wagers and everybody fears: the hand's winner picks who eats it. It costs the same
	// small amount whatever kinds the eater has met before — and then rolls against the total, so it can
	// end somebody outright rather than only bringing them closer to it.
	//
	// That roll is what makes naming a victim a decision worth making in public: feeding the player at
	// 70% is an attempt on their life, and feeding the one at 10% is a nudge.
	[CreateAssetMenu(fileName = "ItemEffect_Colorful", menuName = "Game/Poker/Item Effects/Colorful")]
	public class PokerItemColorfulEffect : PokerItemEffect
	{
		[Tooltip("Points added before the roll. Flat: this cap does not care which kinds the eater has already met.")]
		[MinValue(0)]
		[SerializeField] private int _gain = 10;

		[Tooltip("On, the roll is made against the rate *after* the gain, which is what the design document describes.")]
		[SerializeField] private bool _rollAfterGain = true;

		protected override void OnConsumeServer(PokerGameMode gameMode, PokerPlayer eater, PokerItemType itemType)
		{
			var data = eater.Data;

			var rateBefore = data.HallucinationRate.Value;
			data.ServerChangeHallucination(_gain);

			var against = _rollAfterGain ? data.HallucinationRate.Value : rateBefore;
			if (against <= 0) return;

			// Already at the ceiling is not a roll to make: they are gone either way, and rolling would
			// only invite a reading where the highest rate somehow survives.
			if (!data.IsAlive) return;

			// Going under is written as the ceiling rather than as a flag, so IsAlive keeps being the one
			// question anything asks and a later sobering could still bring them back.
			if (Random.Range(0, 100) < against) data.ServerChangeHallucination(PokerPlayerData.MaxHallucination);
		}
	}
}
