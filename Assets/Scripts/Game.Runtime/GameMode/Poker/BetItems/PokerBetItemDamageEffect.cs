using Game.Runtime.GameMode.Poker.Player;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.BetItems
{
	// The poison cap: eating one costs blood. Routed through ServerChangeHealth like every other
	// source of harm, so the clamp, the finger count and dying at zero all come for free.
	[CreateAssetMenu(fileName = "BetItemEffect_Damage", menuName = "Game/Poker/Bet Item Effects/Damage")]
	public class PokerBetItemDamageEffect : PokerBetItemEffect
	{
		[Tooltip("Blood one bite takes. Per unit eaten — three of these in the pot is three bites.")]
		[MinValue(0)]
		[SerializeField] private int _damage = 1;

		protected override void OnConsumeServer(PokerGameMode gameMode, PokerPlayer eater, PokerBetItemType itemType)
		{
			eater.Data.ServerChangeHealth(-Mathf.Max(0, _damage));
		}
	}
}
