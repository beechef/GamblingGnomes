using Game.Runtime.GameMode.Poker.Player;
using Sirenix.OdinInspector;
using UnityEngine;

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
		[SerializeField] private string _jokerVerb = "GOT A JOKER";

		[Tooltip("What the user reads when it does not.")]
		[SerializeField] private string _failedVerb = "NO JOKER";

		protected override PokerCardFlickerFaces FlickerFaces => PokerCardFlickerFaces.Joker;

		public override string GetOutcomeVerb(int outcome) => outcome != 0 ? _jokerVerb : _failedVerb;

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
