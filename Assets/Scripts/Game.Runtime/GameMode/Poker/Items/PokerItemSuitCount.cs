using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Items
{
	// Counts what is left of each suit in the undealt deck and tells only the user. The count is a
	// snapshot taken at use and stays in front of them until the hand is put away.
	[CreateAssetMenu(fileName = "PokerItem_DeckCount", menuName = "Game/Poker/Items/Suit Count")]
	public class PokerItemSuitCount : PokerItem
	{
		protected override PokerItemAvailability OnGetAvailability(in PokerItemContext context)
		{
			var knowledge = context.User ? context.User.ItemKnowledge : null;
			if (!knowledge) return PokerItemAvailability.Hidden("Nowhere to keep the count.");

			return knowledge.SuitCounts.Value.IsKnown
				? PokerItemAvailability.Dimmed("You already counted this hand.")
				: PokerItemAvailability.Usable;
		}

		protected override void OnUseServer(in PokerItemContext context, in PokerItemUseRequest request)
		{
			var deck = context.GameMode.Deck;

			context.User.ItemKnowledge.ServerSetSuitCounts(new PokerSuitCounts
			{
				Clubs = (byte)deck.CountRemaining(CardSuit.Clubs),
				Diamonds = (byte)deck.CountRemaining(CardSuit.Diamonds),
				Hearts = (byte)deck.CountRemaining(CardSuit.Hearts),
				Spades = (byte)deck.CountRemaining(CardSuit.Spades)
			});
		}
	}
}
