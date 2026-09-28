using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Items
{
	// One of the user's cards goes onto the board face up, one more shared card for everybody, and the user
	// draws a fresh card off the deck in its place. Played as a trade so both cards fly: the drawn card is laid
	// at the end of the board face down, the two change places, and the user's old card is turned up there.
	[CreateAssetMenu(fileName = "PokerItem_PlaceOnBoard", menuName = "Game/Poker/Items/Place On Board")]
	public class PokerItemPlaceOnBoard : PokerItem
	{
		[Header("Card")]
		[Tooltip("Chosen: the user points at the card they lay down. Random: the table draws one of theirs.")]
		[SerializeField] private PokerChoiceMode _ownChoice = PokerChoiceMode.Chosen;

		[Tooltip("Seconds the drawn card lies on the board before the two trade places, long enough for it to land.")]
		[Min(0f)]
		[SerializeField] private float _landHold = 0.8f;

		protected override void OnCollectTargetSteps(List<PokerItemTargetKind> steps)
		{
			if (_ownChoice == PokerChoiceMode.Chosen) steps.Add(PokerItemTargetKind.OwnCard);
		}

		public override string GetTargetPrompt(PokerItemTargetKind kind) =>
			kind == PokerItemTargetKind.OwnCard ? "POINT AT THE CARD YOU LAY ON THE BOARD" : base.GetTargetPrompt(kind);

		protected override PokerItemAvailability OnGetAvailability(in PokerItemContext context)
		{
			var data = context.Data;
			if (!data || data.CommunityCards.Count == 0) return PokerItemAvailability.Hidden("This table deals no board.");
			if (!context.User || !context.User.Data.IsInHand) return PokerItemAvailability.Dimmed("You are not in this hand.");
			if (data.CommunityCards.Count >= 31) return PokerItemAvailability.Dimmed("The board is full.");
			// The replicated count: the deck itself is on the server only.
			if (data.DeckRemaining.Value <= 0) return PokerItemAvailability.Dimmed("The deck is empty.");

			for (var slot = 0; slot < context.User.Data.CardCount; slot++)
			{
				if (AcceptsOwnCard(context, slot)) return PokerItemAvailability.Usable;
			}

			return PokerItemAvailability.Dimmed("You have no card to lay down.");
		}

		public override bool AcceptsOwnCard(in PokerItemContext context, int slot)
		{
			var data = context.User ? context.User.Data : null;
			return data && data.IsInHand && slot >= 0 && slot < data.CardCount;
		}

		protected override async Awaitable OnUseServerAsync(PokerItemContext context, PokerItemUseRequest request, CancellationToken ct)
		{
			var user = context.User;
			var local = context;

			var ownSlot = _ownChoice == PokerChoiceMode.Chosen
				? request.OwnSlot
				: PokerItemModule.PickRandomSlot(user.Data.CardCount, slot => AcceptsOwnCard(local, slot));

			if (!AcceptsOwnCard(context, ownSlot)) return;

			var drawn = context.GameMode.Deck.Draw();
			if (!drawn.IsValid) return;

			// Withheld while it lies there: it is the user's card, and a street ending mid-trade must not turn it.
			var gameMode = context.GameMode;
			var boardSlot = gameMode.ServerAddWithheldCommunityCard(drawn);
			if (boardSlot < 0) return;

			try
			{
				if (_landHold > 0f) await Awaitable.WaitForSecondsAsync(_landHold, ct);

				if (!await context.Module.ServerExchangeCardsAsync(
					    PokerCardPlace.InHand(user.ClientId, ownSlot), PokerCardPlace.OnBoard(boardSlot), ct, flyFaceDown: true)) return;

				// The card drawn is the user's own to see; the one laid down is everybody's.
				user.Data.ServerMarkLookedAt(ownSlot);
				gameMode.ServerRevealCommunityCard(boardSlot);
			}
			finally
			{
				gameMode.ServerReleaseCommunityCard(boardSlot);
			}
		}
	}
}
