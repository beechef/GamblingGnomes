using System.Collections.Generic;
using System.Threading;
using Game.Runtime.GameMode.Poker.Player;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Items
{
	// A face-down board card is turned up for everyone, held there long enough to be read, then traded with
	// one of the user's cards. The user's old card now lies face up on the board, and the whole table saw
	// which card went into their hand.
	[CreateAssetMenu(fileName = "PokerItem_SwapBoard", menuName = "Game/Poker/Items/Swap Board")]
	public class PokerItemSwapBoard : PokerItem
	{
		[Header("Swap")]
		[Tooltip("Chosen: the user points at the card they give. Random: the table draws it.")]
		[SerializeField] private PokerChoiceMode _ownChoice = PokerChoiceMode.Chosen;

		[Tooltip("Chosen: the user points at the face-down board card. Random: the table draws one.")]
		[SerializeField] private PokerChoiceMode _slotChoice = PokerChoiceMode.Random;

		[Tooltip("Seconds the board card lies face up before the two cards trade places.")]
		[Min(0f)]
		[SerializeField] private float _revealHold = 1.2f;

		protected override void OnCollectTargetSteps(List<PokerItemTargetKind> steps)
		{
			if (_ownChoice == PokerChoiceMode.Chosen) steps.Add(PokerItemTargetKind.OwnCard);
			if (_slotChoice == PokerChoiceMode.Chosen) steps.Add(PokerItemTargetKind.BoardCard);
		}

		public override string GetTargetPrompt(PokerItemTargetKind kind) => kind switch
		{
			PokerItemTargetKind.OwnCard => "POINT AT THE CARD YOU PUT ON THE BOARD",
			_ => base.GetTargetPrompt(kind)
		};

		protected override PokerItemAvailability OnGetAvailability(in PokerItemContext context)
		{
			var data = context.Data;
			if (!data || data.CommunityCards.Count == 0) return PokerItemAvailability.Hidden("This table deals no board.");
			if (!context.User || !context.User.Data.IsInHand) return PokerItemAvailability.Dimmed("You are not in this hand.");

			if (FirstFaceDown(data) < 0) return PokerItemAvailability.Hidden("Every board card is already face up.");

			for (var slot = 0; slot < context.User.Data.CardCount; slot++)
			{
				if (AcceptsOwnCard(context, slot)) return PokerItemAvailability.Usable;
			}

			return PokerItemAvailability.Dimmed("You have no card left to give.");
		}

		public override bool AcceptsOwnCard(in PokerItemContext context, int slot)
		{
			var data = context.User ? context.User.Data : null;
			return data && data.IsInHand && slot >= 0 && slot < data.CardCount && !data.IsHoleCardShown(slot);
		}

		public override bool AcceptsBoardCard(in PokerItemContext context, int slot)
		{
			var data = context.Data;
			return data && slot >= 0 && slot < data.CommunityCards.Count && !data.IsCommunityCardRevealed(slot);
		}

		protected override async Awaitable OnUseServerAsync(PokerItemContext context, PokerItemUseRequest request, CancellationToken ct)
		{
			var user = context.User;
			var data = context.Data;
			var local = context;

			var ownSlot = _ownChoice == PokerChoiceMode.Chosen
				? request.OwnSlot
				: PokerItemModule.PickRandomSlot(user.Data.CardCount, slot => AcceptsOwnCard(local, slot));

			var boardSlot = _slotChoice == PokerChoiceMode.Chosen
				? request.CardSlot
				: PokerItemModule.PickRandomSlot(data.CommunityCards.Count, slot => AcceptsBoardCard(local, slot));

			if (ownSlot < 0 || boardSlot < 0) return;

			context.GameMode.ServerRevealCommunityCard(boardSlot);

			if (_revealHold > 0f) await Awaitable.WaitForSecondsAsync(_revealHold, ct);

			if (!AcceptsOwnCard(context, ownSlot)) return;

			var taken = data.CommunityCards[boardSlot];

			if (!await context.Module.ServerExchangeCardsAsync(
				    PokerCardPlace.InHand(user.ClientId, ownSlot), PokerCardPlace.OnBoard(boardSlot), ct)) return;

			user.Data.ServerMarkLookedAt(ownSlot);

			// Everybody watched it go into the user's hand.
			foreach (var player in context.GameMode.SeatedPlayers)
			{
				if (player && player != user && player.ItemKnowledge) player.ItemKnowledge.ServerLearn(user.ClientId, ownSlot, taken);
			}
		}

		private static int FirstFaceDown(PokerGameData data)
		{
			for (var slot = 0; slot < data.CommunityCards.Count; slot++)
			{
				if (!data.IsCommunityCardRevealed(slot)) return slot;
			}

			return -1;
		}
	}
}
