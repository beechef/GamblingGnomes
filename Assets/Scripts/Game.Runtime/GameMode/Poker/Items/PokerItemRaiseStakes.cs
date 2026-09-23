using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Items
{
	// Every bet on the next street puts up more caps, the user's own included. Two played on one street add up.
	[CreateAssetMenu(fileName = "PokerItem_RaiseStakes", menuName = "Game/Poker/Items/Raise Stakes")]
	public class PokerItemRaiseStakes : PokerItem
	{
		[Header("Raise")]
		[Tooltip("Caps added to every bet on the next street.")]
		[MinValue(1)]
		[SerializeField] private int _extraStake = 1;

		protected override PokerItemAvailability OnGetAvailability(in PokerItemContext context) =>
			context.Module.HasNextStreet()
				? PokerItemAvailability.Usable
				: PokerItemAvailability.Hidden("No street is left in this hand.");

		protected override void OnUseServer(in PokerItemContext context, in PokerItemUseRequest request)
		{
			var module = context.Module;
			module.ServerAddRule(PokerItemTableRuleKind.ExtraStake, module.StreetSerial.Value + 1, Mathf.Max(1, _extraStake), context.User.ClientId);
		}
	}
}
