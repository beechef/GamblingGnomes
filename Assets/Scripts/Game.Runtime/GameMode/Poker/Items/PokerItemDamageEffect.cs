using Game.Runtime.GameMode.Poker.Player;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Items
{
	// The poison cap: eating one costs blood. Routed through ServerChangeHealth like every other
	// source of harm, so the clamp, the finger count and dying at zero all come for free.
	[CreateAssetMenu(fileName = "ItemEffect_Damage", menuName = "Game/Poker/Item Effects/Damage")]
	public class PokerItemDamageEffect : PokerItemEffect
	{
		[Tooltip("Blood one bite takes. Per unit eaten — three of these in the pot is three bites.")]
		[MinValue(0)]
		[SerializeField] private int _damage = 1;

		protected override void OnConsumeServer(PokerGameMode gameMode, PokerPlayer eater, byte itemType)
		{
			eater.Data.ServerChangeHealth(-Mathf.Max(0, _damage));
		}
	}
}
