using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Items
{
	// Nobody may fold on the next street, the user included. Optionally the all-in round that street can open
	// is locked too, so going all in there becomes the only answer.
	[CreateAssetMenu(fileName = "PokerItem_LockFold", menuName = "Game/Poker/Items/Lock Fold")]
	public class PokerItemLockFold : PokerItem
	{
		[Header("Lock")]
		[Tooltip("On, the all-in round that follows the locked street cannot be folded out of either.")]
		[SerializeField] private bool _affectsAllIn;

		protected override PokerItemAvailability OnGetAvailability(in PokerItemContext context) =>
			context.Module.HasNextStreet()
				? PokerItemAvailability.Usable
				: PokerItemAvailability.Hidden("No street is left in this hand.");

		protected override void OnUseServer(in PokerItemContext context, in PokerItemUseRequest request)
		{
			var module = context.Module;
			var street = module.StreetSerial.Value + 1;

			module.ServerAddRule(PokerItemTableRuleKind.NoFold, street, 0, context.User.ClientId);
			if (_affectsAllIn) module.ServerAddRule(PokerItemTableRuleKind.NoFoldAllIn, street, 0, context.User.ClientId);
		}
	}
}
