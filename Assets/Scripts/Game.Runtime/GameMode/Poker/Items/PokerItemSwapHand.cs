using System.Collections.Generic;
using System.Threading;
using Game.Runtime.GameMode.Poker.Player;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Items
{
	// The user and a player they choose trade one card. The other player picks which of theirs goes; both
	// cards cross the table together, land in their new holder's hand as looked at, and each side knows
	// where the card they gave went.
	[CreateAssetMenu(fileName = "PokerItem_SwapHand", menuName = "Game/Poker/Items/Swap Hand")]
	public class PokerItemSwapHand : PokerItem
	{
		[Header("Swap")]
		[Tooltip("Chosen: the user points at the card they give. Random: the table draws it.")]
		[SerializeField] private PokerChoiceMode _ownChoice = PokerChoiceMode.Random;

		public override bool NeedsResponse => true;

		protected override void OnCollectTargetSteps(List<PokerItemTargetKind> steps)
		{
			if (_ownChoice == PokerChoiceMode.Chosen) steps.Add(PokerItemTargetKind.OwnCard);
			steps.Add(PokerItemTargetKind.Player);
		}

		public override string GetTargetPrompt(PokerItemTargetKind kind) => kind switch
		{
			PokerItemTargetKind.OwnCard => "POINT AT THE CARD YOU GIVE",
			PokerItemTargetKind.Player => "POINT AT WHO YOU SWAP WITH",
			_ => base.GetTargetPrompt(kind)
		};

		public override string GetResponsePrompt() => "SWAPS A CARD WITH YOU: POINT AT THE ONE YOU GIVE";

		protected override PokerItemAvailability OnGetAvailability(in PokerItemContext context)
		{
			if (!context.User || !context.User.Data.IsInHand) return PokerItemAvailability.Dimmed("You are not in this hand.");
			if (!HasTradeableCard(context.User)) return PokerItemAvailability.Dimmed("You have no card left to give.");

			foreach (var player in context.GameMode.SeatedPlayers)
			{
				if (AcceptsPlayer(context, player)) return PokerItemAvailability.Usable;
			}

			return PokerItemAvailability.Dimmed("Nobody else has a card to swap.");
		}

		public override bool AcceptsOwnCard(in PokerItemContext context, int slot) => IsTradeable(context.User, slot);

		public override bool AcceptsPlayer(in PokerItemContext context, PokerPlayer target) =>
			target && target.Data && target != context.User && target.Data.IsInHand && HasTradeableCard(target);

		public override bool AcceptsResponseCard(in PokerItemContext context, PokerPlayer responder, int slot) => IsTradeable(responder, slot);

		protected override async Awaitable OnUseServerAsync(PokerItemContext context, PokerItemUseRequest request, CancellationToken ct)
		{
			var user = context.User;
			var target = context.GameMode.FindSeatedPlayerAtSeat(request.TargetSeat);

			var ownSlot = _ownChoice == PokerChoiceMode.Chosen
				? request.OwnSlot
				: PokerItemModule.PickRandomSlot(user.Data.CardCount, slot => IsTradeable(user, slot));

			var targetSlot = await context.Module.ServerAskForCardAsync(context, this, target, ct);

			// Either may have folded or gone under while the other was choosing; then there is nobody to trade with.
			if (!IsTradeable(user, ownSlot) || !IsTradeable(target, targetSlot)) return;

			var given = user.Data.HoleCards[ownSlot];
			var taken = target.Data.HoleCards[targetSlot];

			var userPlace = PokerCardPlace.InHand(user.ClientId, ownSlot);
			var targetPlace = PokerCardPlace.InHand(target.ClientId, targetSlot);

			if (!await context.Module.ServerExchangeCardsAsync(userPlace, targetPlace, ct)) return;

			user.Data.ServerMarkLookedAt(ownSlot);
			target.Data.ServerMarkLookedAt(targetSlot);

			// Each knows what they handed over and where it now lies, and the new holder knows they know.
			user.ItemKnowledge.ServerLearn(target.ClientId, targetSlot, given);
			target.ItemKnowledge.ServerRecordExposure(user.ClientId, targetSlot, given);
			target.ItemKnowledge.ServerLearn(user.ClientId, ownSlot, taken);
			user.ItemKnowledge.ServerRecordExposure(target.ClientId, ownSlot, taken);
		}

		private static bool HasTradeableCard(PokerPlayer player)
		{
			if (!player || !player.Data) return false;

			for (var slot = 0; slot < player.Data.CardCount; slot++)
			{
				if (IsTradeable(player, slot)) return true;
			}

			return false;
		}

		// Not one the whole table already sees: a card lying face up is out of the game of hiding.
		private static bool IsTradeable(PokerPlayer player, int slot) =>
			player && player.Data && player.Data.IsInHand && slot >= 0 && slot < player.Data.CardCount && !player.Data.IsHoleCardShown(slot);
	}
}
