using System.Collections.Generic;
using System.Threading;
using Game.Runtime.GameMode.Poker.Player;
using Localization;
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
			kind == PokerItemTargetKind.OwnCard ? Localizer.Get(LocalizationKeys.Item.RandomSuit.Prompt) : base.GetTargetPrompt(kind);

		protected override PokerItemAvailability OnGetAvailability(in PokerItemContext context)
		{
			if (!context.User || !context.User.Data.IsInHand) return PokerItemAvailability.Dimmed(Localizer.Get(LocalizationKeys.Item.Reason.NotInHand));

			for (var slot = 0; slot < context.User.Data.CardCount; slot++)
			{
				if (AcceptsOwnCard(context, slot)) return PokerItemAvailability.Usable;
			}

			return PokerItemAvailability.Dimmed(Localizer.Get(LocalizationKeys.Item.Reason.CantChange));
		}

		public override bool AcceptsOwnCard(in PokerItemContext context, int slot)
		{
			var data = context.User ? context.User.Data : null;
			if (!data || !data.IsInHand || slot < 0 || slot >= data.CardCount) return false;

			var card = data.HoleCards[slot];
			return card.IsValid && !card.IsJoker && CanRewrite(data, slot);
		}

		// Which faces the card flickers through while it changes.
		protected abstract PokerCardFlickerFaces FlickerFaces { get; }

		protected override async Awaitable OnUseServerAsync(PokerItemContext context, PokerItemUseRequest request, CancellationToken ct)
		{
			var user = context.User;

			var slot = _cardChoice == PokerChoiceMode.Chosen
				? request.OwnSlot
				: PokerItemModule.PickRandomSlot(user.Data.CardCount, s => AcceptsOwnCard(context, s));

			if (!AcceptsOwnCard(context, slot)) return;

			if (!TryRewrite(user.Data, slot, out var card)) card = CardData.None;

			await context.Module.ServerRewriteHoleCardAsync(user, slot, card, FlickerFaces, ct);

			OnRewriteSettled(context, card.IsValid);
		}

		protected virtual bool CanRewrite(PokerPlayerData holder, int slot) => true;

		// False leaves the card as it was.
		protected abstract bool TryRewrite(PokerPlayerData holder, int slot, out CardData card);

		protected virtual void OnRewriteSettled(in PokerItemContext context, bool rewritten) { }
	}
}
