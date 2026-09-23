using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Items
{
	// One more card off the undealt deck, straight into the user's hand, at the price of not folding again
	// this hand. The showdown still scores the best five of whatever they hold.
	[CreateAssetMenu(fileName = "PokerItem_ExtraDraw", menuName = "Game/Poker/Items/Extra Draw")]
	public class PokerItemExtraDraw : PokerItem
	{
		protected override PokerItemAvailability OnGetAvailability(in PokerItemContext context)
		{
			if (!context.User || !context.User.Data.IsInHand) return PokerItemAvailability.Dimmed("You are not in this hand.");
			if (context.User.Data.CardCount >= 31) return PokerItemAvailability.Dimmed("Your hand is full.");
			if (context.GameMode.Deck.Remaining <= 0) return PokerItemAvailability.Dimmed("The deck is empty.");

			return PokerItemAvailability.Usable;
		}

		protected override void OnUseServer(in PokerItemContext context, in PokerItemUseRequest request)
		{
			var card = context.GameMode.Deck.Draw();
			if (!card.IsValid) return;

			context.User.Data.ServerDrawHoleCard(card);

			var module = context.Module;
			module.ServerAddRule(PokerItemTableRuleKind.NoFoldSelfForHand, module.StreetSerial.Value, 0, context.User.ClientId);
		}
	}
}
