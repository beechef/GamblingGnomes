using System.Collections.Generic;
using Localization;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Items
{
	// See one board card the streets have not turned yet, for the street it is played on only (it turns back
	// down when that street ends), at the price of not folding on that street. Only the user sees it; the table hears that a board card was looked at.
	[CreateAssetMenu(fileName = "PokerItem_PeekBoard", menuName = "Game/Poker/Items/Peek Board")]
	public class PokerItemPeekBoard : PokerItem
	{
		[Header("Peek")]
		[Tooltip("Chosen: the user points at the board card. Random: the table draws one of the face-down cards. LastFaceDown: the last card the streets will turn, the river.")]
		[SerializeField] private PokerBoardPeekSlot _slotChoice = PokerBoardPeekSlot.Chosen;

		protected override void OnCollectTargetSteps(List<PokerItemTargetKind> steps)
		{
			if (_slotChoice == PokerBoardPeekSlot.Chosen) steps.Add(PokerItemTargetKind.BoardCard);
		}

		protected override PokerItemAvailability OnGetAvailability(in PokerItemContext context)
		{
			var board = context.Data.CommunityCards;
			if (board.Count == 0) return PokerItemAvailability.Hidden(Localizer.Get(LocalizationKeys.Item.Reason.NoBoard));

			if (_slotChoice == PokerBoardPeekSlot.LastFaceDown)
			{
				return AcceptsBoardCard(context, LastFaceDownSlot(context.Data))
					? PokerItemAvailability.Usable
					: PokerItemAvailability.Hidden(Localizer.Get(LocalizationKeys.Item.Reason.LastBoardSeen));
			}

			for (var slot = 0; slot < board.Count; slot++)
			{
				if (AcceptsBoardCard(context, slot)) return PokerItemAvailability.Usable;
			}

			return PokerItemAvailability.Hidden(Localizer.Get(LocalizationKeys.Item.Reason.BoardSeen));
		}

		public override bool AcceptsBoardCard(in PokerItemContext context, int slot)
		{
			var data = context.Data;
			if (!data || slot < 0 || slot >= data.CommunityCards.Count || data.IsCommunityCardRevealed(slot)) return false;

			var knowledge = context.User ? context.User.ItemKnowledge : null;
			return knowledge && !knowledge.Knows(PokerGameData.NoTurn, slot);
		}

		protected override void OnUseServer(in PokerItemContext context, in PokerItemUseRequest request)
		{
			var local = context;

			var slot = _slotChoice switch
			{
				PokerBoardPeekSlot.Chosen => request.CardSlot,
				PokerBoardPeekSlot.LastFaceDown => LastFaceDownSlot(context.Data),
				_ => PokerItemModule.PickRandomSlot(context.Data.CommunityCards.Count, s => AcceptsBoardCard(local, s))
			};

			if (slot < 0 || !AcceptsBoardCard(context, slot)) return;

			context.User.ItemKnowledge.ServerLearn(PokerGameData.NoTurn, slot, context.Data.CommunityCards[slot]);

			var module = context.Module;
			module.ServerAddRule(PokerItemTableRuleKind.NoFoldSelf, module.StreetSerial.Value, 0, context.User.ClientId);
		}

		// The streets turn the board left to right, so the highest face-down slot is the last one turned.
		private static int LastFaceDownSlot(PokerGameData data)
		{
			if (!data) return -1;

			for (var slot = Mathf.Min(data.CommunityCards.Count, 31) - 1; slot >= 0; slot--)
			{
				if (!data.IsCommunityCardRevealed(slot)) return slot;
			}

			return -1;
		}
	}
}
