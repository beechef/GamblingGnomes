using Game.Runtime.GameMode.Poker.Player;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Items
{
	// A gamble on the user's own hand: at the item's odds the chosen card becomes a copy of one of their other
	// cards, drawn at random; otherwise nothing changes. The table hears whether it took, never which card.
	[CreateAssetMenu(fileName = "PokerItem_CopyCard", menuName = "Game/Poker/Items/Copy Card")]
	public class PokerItemCopyCard : PokerItemRewriteCard
	{
		[Header("Copy")]
		[Tooltip("Chance, in percent, that the card becomes a copy.")]
		[PropertyRange(0, 100)]
		[SerializeField] private int _chance = 35;

		[Tooltip("What the table reads when the copy takes.")]
		[SerializeField] private string _copiedVerb = "COPIED A CARD";

		[Tooltip("What the table reads when it does not.")]
		[SerializeField] private string _failedVerb = "FAILED TO COPY A CARD";

		public override string GetOutcomeVerb(int outcome) => outcome != 0 ? _copiedVerb : _failedVerb;

		public override bool AnnouncesOutcome => true;

		// Needs another card to copy from.
		protected override bool CanRewrite(PokerPlayerData holder, int slot) => holder.CardCount > 1;

		protected override bool TryRewrite(PokerPlayerData holder, int slot, out CardData card)
		{
			card = CardData.None;
			if (Random.Range(0, 100) >= _chance) return false;

			var source = PokerItemModule.PickRandomSlot(holder.CardCount, s => s != slot && holder.HoleCards[s].IsValid);
			if (source < 0) return false;

			card = holder.HoleCards[source];
			return true;
		}

		protected override void OnRewriteSettled(in PokerItemContext context, bool rewritten)
		{
			var notices = context.GameMode.Notices;
			if (notices) notices.ServerAnnounce(PokerNotice.ForItemOutcome(context.User.ClientId, Type, PokerGameData.NoTurn, rewritten ? 1 : 0));
		}
	}
}
