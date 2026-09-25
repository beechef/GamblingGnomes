using UnityEngine;

namespace Game.Runtime.GameMode.Poker
{
	// When the deal happens, in one place: the deck's animation reads how each card waits and flies, and the
	// deal stage waits exactly as long as that takes. The stage used to carry its own guess, which a fuller
	// table outgrew and the next beat opened with cards still in the air.
	[CreateAssetMenu(fileName = "PokerDealPacing", menuName = "Game/Poker/Deal Pacing")]
	public class PokerDealPacing : ScriptableObject
	{
		[Tooltip("Seconds between one card leaving the deck and the next.")]
		[field: SerializeField, Min(0f)] public float CardInterval { get; private set; } = 0.1f;

		[Tooltip("Seconds a card spends in the air.")]
		[field: SerializeField, Min(0.01f)] public float CardFlight { get; private set; } = 0.4f;

		[Tooltip("Extra seconds between one round of the deal and the next when the cards go straight into the hand. A player's cards then leave far enough apart that one never flies in through the other.")]
		[field: SerializeField, Min(0f)] public float IntoHandRoundGap { get; private set; } = 0.2f;

		[Tooltip("Seconds the dealt table is left to be seen after the last card lands, before the next beat.")]
		[field: SerializeField, Min(0f)] public float Rest { get; private set; } = 0.5f;

		// One card to each player in turn before anybody gets a second.
		public float DelayFor(int slot, int order, int players, bool intoHand = false) =>
			(slot * Mathf.Max(1, players) + order) * CardInterval + (intoHand ? slot * IntoHandRoundGap : 0f);

		// The board goes out after every hand, one card at a time, as though it were one more round of the deal.
		public float BoardDelayFor(int boardIndex, int cardsPerPlayer, int players, bool intoHand = false) =>
			DelayFor(cardsPerPlayer, 0, players, intoHand) + boardIndex * CardInterval;

		public float DealDuration(int players, int cardsPerPlayer, int boardCards = 0, bool intoHand = false)
		{
			if (boardCards > 0) return BoardDelayFor(boardCards - 1, Mathf.Max(0, cardsPerPlayer), Mathf.Max(0, players), intoHand) + CardFlight + Rest;

			if (players <= 0 || cardsPerPlayer <= 0) return Rest;

			return DelayFor(cardsPerPlayer - 1, players - 1, players, intoHand) + CardFlight + Rest;
		}
	}
}
