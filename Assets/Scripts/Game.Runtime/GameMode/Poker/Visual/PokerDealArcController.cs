using DG.Tweening;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Visual
{
	// A dealer's deal: one card to each player in turn before anybody gets a second, each tossed in an arc
	// off the top of the deck.
	public class PokerDealArcController : PokerDealController
	{
		[Tooltip("Seconds between one card leaving the deck and the next. Times every card in the deal must fit inside the deal stage's own duration, or the next beat opens with cards still in the air.")]
		[Min(0f)]
		[SerializeField] private float _interval = 0.1f;

		[Tooltip("Seconds a card spends in the air.")]
		[Min(0.01f)]
		[SerializeField] private float _duration = 0.4f;

		[SerializeField] private Ease _ease = Ease.OutCubic;

		[Tooltip("How high the card rises on its way, along the table's up.")]
		[SerializeField] private float _arc = 0.08f;

		public override float DelayFor(PokerDealTurn turn) => (turn.Slot * turn.Players + turn.Order) * _interval;

		public override Tween Travel(Transform card, Vector3 localPosition, Quaternion localRotation)
			=> PokerCardVisual.ArcTween(card, localPosition, localRotation, _duration, _ease, _arc);
	}
}
