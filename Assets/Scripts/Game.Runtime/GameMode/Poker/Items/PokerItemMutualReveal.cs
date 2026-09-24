using System.Collections.Generic;
using System.Threading;
using Game.Runtime.GameMode.Poker.Player;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Items
{
	// The user and a player they choose each turn one card face up for the whole table, until the hand ends.
	// The other player picks which of theirs; both cards go over together once they have.
	[CreateAssetMenu(fileName = "PokerItem_MutualReveal", menuName = "Game/Poker/Items/Mutual Reveal")]
	public class PokerItemMutualReveal : PokerItem
	{
		[Header("Reveal")]
		[Tooltip("Chosen: the user points at the card they show. Random: the table draws it.")]
		[SerializeField] private PokerChoiceMode _ownChoice = PokerChoiceMode.Chosen;

		public override bool NeedsResponse => true;

		protected override void OnCollectTargetSteps(List<PokerItemTargetKind> steps)
		{
			if (_ownChoice == PokerChoiceMode.Chosen) steps.Add(PokerItemTargetKind.OwnCard);
			steps.Add(PokerItemTargetKind.Player);
		}

		public override string GetTargetPrompt(PokerItemTargetKind kind) => kind switch
		{
			PokerItemTargetKind.OwnCard => "POINT AT THE CARD YOU WILL SHOW",
			PokerItemTargetKind.Player => "POINT AT WHO SHOWS ONE WITH YOU",
			_ => base.GetTargetPrompt(kind)
		};

		public override string GetResponsePrompt() => "WANTS YOU TO SHOW A CARD: POINT AT ONE";

		protected override PokerItemAvailability OnGetAvailability(in PokerItemContext context)
		{
			if (!context.User || !context.User.Data.IsInHand) return PokerItemAvailability.Dimmed("You are not in this hand.");
			if (!HasHiddenCard(context.User)) return PokerItemAvailability.Dimmed("Your cards are already face up.");

			foreach (var player in context.GameMode.SeatedPlayers)
			{
				if (AcceptsPlayer(context, player)) return PokerItemAvailability.Usable;
			}

			return PokerItemAvailability.Dimmed("Nobody else has a card left to show.");
		}

		public override bool AcceptsOwnCard(in PokerItemContext context, int slot) => IsHidden(context.User, slot);

		public override bool AcceptsPlayer(in PokerItemContext context, PokerPlayer target) =>
			target && target.Data && target != context.User && target.Data.IsInHand && HasHiddenCard(target);

		public override bool AcceptsResponseCard(in PokerItemContext context, PokerPlayer responder, int slot) => IsHidden(responder, slot);

		protected override async Awaitable OnUseServerAsync(PokerItemContext context, PokerItemUseRequest request, CancellationToken ct)
		{
			var user = context.User;
			var target = context.GameMode.FindSeatedPlayerAtSeat(request.TargetSeat);

			var ownSlot = _ownChoice == PokerChoiceMode.Chosen
				? request.OwnSlot
				: PokerItemModule.PickRandomSlot(user.Data.CardCount, slot => IsHidden(user, slot));

			var targetSlot = await context.Module.ServerAskForCardAsync(context, this, target, ct);

			// Either may have folded or left while the other was choosing; whoever is still there still shows.
			if (user && user.Data.IsInHand) user.Data.ServerShowHoleCard(ownSlot);
			if (target && target.Data.IsInHand) target.Data.ServerShowHoleCard(targetSlot);
		}

		private static bool HasHiddenCard(PokerPlayer player)
		{
			if (!player || !player.Data) return false;

			for (var slot = 0; slot < player.Data.CardCount; slot++)
			{
				if (IsHidden(player, slot)) return true;
			}

			return false;
		}

		private static bool IsHidden(PokerPlayer player, int slot) =>
			player && player.Data && slot >= 0 && slot < player.Data.CardCount && !player.Data.IsHoleCardShown(slot);
	}
}
