using Game.Runtime.GameMode.Poker.Player;
using Localization;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Serialization;

namespace Game.Runtime.GameMode.Poker.Items
{
	// A gamble on the user's own hand: at the item's odds the chosen card becomes a Joker; otherwise nothing
	// changes. Only the user hears whether it took; the table hears it was used.
	[CreateAssetMenu(fileName = "PokerItem_JokerCard", menuName = "Game/Poker/Items/Joker Card")]
	public class PokerItemJokerCard : PokerItemRewriteCard
	{
		[Header("Joker")]
		[Tooltip("Chance, in percent, that the card becomes a Joker.")]
		[PropertyRange(0, 100)]
		[SerializeField] private int _chance = 50;

		[Tooltip("What the user reads when the card becomes a Joker.")]
		[LocalizationKey]
		[FormerlySerializedAs("_jokerVerb")]
		[SerializeField] private string _jokerKey = LocalizationKeys.Item.JokerCard.Joker;

		[Tooltip("What the user reads when it does not.")]
		[LocalizationKey]
		[FormerlySerializedAs("_failedVerb")]
		[SerializeField] private string _failedKey = LocalizationKeys.Item.JokerCard.Failed;

		protected override PokerCardFlickerFaces FlickerFaces => PokerCardFlickerFaces.Joker;

		public override string GetOutcomeVerb(int outcome) => Localizer.Get(outcome != 0 ? _jokerKey : _failedKey);

		protected override bool TryRewrite(PokerPlayerData holder, int slot, out CardData card)
		{
			card = Random.Range(0, 100) < _chance ? CardData.Joker : CardData.None;
			return card.IsValid;
		}

		protected override void OnRewriteSettled(in PokerItemContext context, bool rewritten)
		{
			var notices = context.GameMode.Notices;
			if (notices) notices.ServerTell(context.User.ClientId, PokerNotice.ForItemOutcome(context.User.ClientId, Type, PokerGameData.NoTurn, rewritten ? 1 : 0));
		}
	}
}
