using Game.Runtime.GameMode.Poker.Player;
using Localization;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Serialization;

namespace Game.Runtime.GameMode.Poker.Items
{
	// A gamble on the user's own hand: at the item's odds the chosen card becomes a copy of one of their other
	// cards, drawn at random; otherwise nothing changes. Only the user hears whether it took; the table hears it was used.
	[CreateAssetMenu(fileName = "PokerItem_CopyCard", menuName = "Game/Poker/Items/Copy Card")]
	public class PokerItemCopyCard : PokerItemRewriteCard
	{
		[Header("Copy")]
		[Tooltip("Chance, in percent, that the card becomes a copy.")]
		[PropertyRange(0, 100)]
		[SerializeField] private int _chance = 35;

		[Tooltip("What the user reads when the copy takes.")]
		[LocalizationKey]
		[FormerlySerializedAs("_copiedVerb")]
		[SerializeField] private string _copiedKey = LocalizationKeys.Item.CopyCard.Copied;

		[Tooltip("What the user reads when it does not.")]
		[LocalizationKey]
		[FormerlySerializedAs("_failedVerb")]
		[SerializeField] private string _failedKey = LocalizationKeys.Item.CopyCard.Failed;

		public override string GetOutcomeVerb(int outcome) => Localizer.Get(outcome != 0 ? _copiedKey : _failedKey);

		protected override PokerCardFlickerFaces FlickerFaces => PokerCardFlickerFaces.OwnHand;

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
			if (notices) notices.ServerTell(context.User.ClientId, PokerNotice.ForItemOutcome(context.User.ClientId, Type, PokerGameData.NoTurn, rewritten ? 1 : 0));
		}
	}
}
