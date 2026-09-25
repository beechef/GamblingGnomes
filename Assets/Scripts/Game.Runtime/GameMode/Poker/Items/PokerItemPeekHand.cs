using System.Collections.Generic;
using Game.Runtime.GameMode.Poker.Player;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Items
{
	// See one card of another player still in the hand. The user learns it privately; the player it belonged
	// to is told which card was seen, and the table only that somebody looked.
	[CreateAssetMenu(fileName = "PokerItem_PeekHand", menuName = "Game/Poker/Items/Peek Hand")]
	public class PokerItemPeekHand : PokerItem
	{
		[Header("Peek")]
		[Tooltip("Chosen: the user points at the card. Random: the user points at the player and the table draws one of their cards.")]
		[SerializeField] private PokerChoiceMode _cardChoice = PokerChoiceMode.Chosen;

		protected override void OnCollectTargetSteps(List<PokerItemTargetKind> steps) =>
			steps.Add(_cardChoice == PokerChoiceMode.Chosen ? PokerItemTargetKind.OpponentCard : PokerItemTargetKind.Player);

		protected override PokerItemAvailability OnGetAvailability(in PokerItemContext context)
		{
			foreach (var player in context.GameMode.SeatedPlayers)
			{
				if (AcceptsPlayer(context, player)) return PokerItemAvailability.Usable;
			}

			return PokerItemAvailability.Dimmed("Nobody has a card you have not seen.");
		}

		public override bool AcceptsPlayer(in PokerItemContext context, PokerPlayer target)
		{
			if (!IsOpponentInHand(context, target)) return false;

			for (var slot = 0; slot < target.Data.CardCount; slot++)
			{
				if (IsUnseen(context, target, slot)) return true;
			}

			return false;
		}

		public override bool AcceptsOpponentCard(in PokerItemContext context, PokerPlayer target, int slot) =>
			IsOpponentInHand(context, target) && IsUnseen(context, target, slot);

		protected override void OnUseServer(in PokerItemContext context, in PokerItemUseRequest request)
		{
			var target = context.GameMode.FindSeatedPlayerAtSeat(request.TargetSeat);
			var local = context;

			var slot = _cardChoice == PokerChoiceMode.Chosen
				? request.CardSlot
				: PokerItemModule.PickRandomSlot(target.Data.CardCount, s => IsUnseen(local, target, s));

			if (slot < 0) return;

			var card = target.Data.HoleCards[slot];

			context.User.ItemKnowledge.ServerLearn(target.ClientId, slot, card);
			target.ItemKnowledge.ServerRecordExposure(context.User.ClientId, slot, card);
			context.Module.ServerTell(target.ClientId, PokerNotice.ForItemDetail(context.User.ClientId, Type, card));
		}

		private static bool IsOpponentInHand(in PokerItemContext context, PokerPlayer target) =>
			target && target.Data && context.User && target != context.User && target.Data.IsInHand;

		// A card the user cannot already read: not turned up for the table, not seen through another item.
		private static bool IsUnseen(in PokerItemContext context, PokerPlayer target, int slot)
		{
			if (!target || slot < 0 || slot >= target.Data.CardCount) return false;
			if (target.Data.IsHoleCardShown(slot)) return false;

			var knowledge = context.User ? context.User.ItemKnowledge : null;
			return knowledge && !knowledge.Knows(target.ClientId, slot);
		}
	}
}
