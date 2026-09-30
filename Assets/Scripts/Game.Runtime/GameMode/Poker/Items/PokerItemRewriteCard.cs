using System.Collections.Generic;
using Game.Runtime.GameMode.Poker.Player;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Items
{
	// One of the user's own cards turned into another where it lies. What it becomes, and whether it takes at
	// all, is the subclass's; which card, the gates and the write are shared. A Joker is already everything, so
	// it is never offered.
	public abstract class PokerItemRewriteCard : PokerItem
	{
		[Header("Card")]
		[Tooltip("Chosen: the user points at the card. Random: the table draws one of theirs.")]
		[SerializeField] private PokerChoiceMode _cardChoice = PokerChoiceMode.Chosen;

		protected override void OnCollectTargetSteps(List<PokerItemTargetKind> steps)
		{
			if (_cardChoice == PokerChoiceMode.Chosen) steps.Add(PokerItemTargetKind.OwnCard);
		}

		public override string GetTargetPrompt(PokerItemTargetKind kind) =>
			kind == PokerItemTargetKind.OwnCard ? "POINT AT THE CARD TO CHANGE" : base.GetTargetPrompt(kind);

		protected override PokerItemAvailability OnGetAvailability(in PokerItemContext context)
		{
			if (!context.User || !context.User.Data.IsInHand) return PokerItemAvailability.Dimmed("You are not in this hand.");

			for (var slot = 0; slot < context.User.Data.CardCount; slot++)
			{
				if (AcceptsOwnCard(context, slot)) return PokerItemAvailability.Usable;
			}

			return PokerItemAvailability.Dimmed("None of your cards can be changed.");
		}

		public override bool AcceptsOwnCard(in PokerItemContext context, int slot)
		{
			var data = context.User ? context.User.Data : null;
			if (!data || !data.IsInHand || slot < 0 || slot >= data.CardCount) return false;

			var card = data.HoleCards[slot];
			return card.IsValid && !card.IsJoker && CanRewrite(data, slot);
		}

		protected override void OnUseServer(in PokerItemContext context, in PokerItemUseRequest request)
		{
			var local = context;
			var user = context.User;

			var slot = _cardChoice == PokerChoiceMode.Chosen
				? request.OwnSlot
				: PokerItemModule.PickRandomSlot(user.Data.CardCount, s => AcceptsOwnCard(local, s));

			if (!AcceptsOwnCard(context, slot)) return;

			if (TryRewrite(user.Data, slot, out var card)) context.Module.ServerRewriteHoleCard(user, slot, card);

			OnRewriteSettled(context, card.IsValid);
		}

		protected virtual bool CanRewrite(PokerPlayerData holder, int slot) => true;

		// False leaves the card as it was.
		protected abstract bool TryRewrite(PokerPlayerData holder, int slot, out CardData card);

		protected virtual void OnRewriteSettled(in PokerItemContext context, bool rewritten) { }
	}
}
