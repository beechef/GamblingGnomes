using System.Collections.Generic;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Items
{
	// See one board card the streets have not turned yet, at the price of not folding for the rest of this
	// street. Only the user sees it; the table hears that a board card was looked at.
	[CreateAssetMenu(fileName = "PokerItem_PeekBoard", menuName = "Game/Poker/Items/Peek Board")]
	public class PokerItemPeekBoard : PokerItem
	{
		[Header("Peek")]
		[Tooltip("Chosen: the user points at the board card. Random: the table draws one of the face-down cards.")]
		[SerializeField] private PokerChoiceMode _slotChoice = PokerChoiceMode.Chosen;

		protected override void OnCollectTargetSteps(List<PokerItemTargetKind> steps)
		{
			if (_slotChoice == PokerChoiceMode.Chosen) steps.Add(PokerItemTargetKind.BoardCard);
		}

		protected override PokerItemAvailability OnGetAvailability(in PokerItemContext context)
		{
			var board = context.Data.CommunityCards;
			if (board.Count == 0) return PokerItemAvailability.Hidden("This table deals no board.");

			for (var slot = 0; slot < board.Count; slot++)
			{
				if (AcceptsBoardCard(context, slot)) return PokerItemAvailability.Usable;
			}

			return PokerItemAvailability.Hidden("Every board card is already face up to you.");
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

			var slot = _slotChoice == PokerChoiceMode.Chosen
				? request.CardSlot
				: PokerItemModule.PickRandomSlot(context.Data.CommunityCards.Count, s => AcceptsBoardCard(local, s));

			if (slot < 0) return;

			context.User.ItemKnowledge.ServerLearn(PokerGameData.NoTurn, slot, context.Data.CommunityCards[slot]);

			var module = context.Module;
			module.ServerAddRule(PokerItemTableRuleKind.NoFoldSelf, module.StreetSerial.Value, 0, context.User.ClientId);
		}
	}
}
