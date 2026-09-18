using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Visual
{
	// A dealer's deal: one card to each player in turn before anybody gets a second, each tossed in an arc
	// off the top of the deck.
	public class PokerDealArcController : PokerDealController
	{
		[Tooltip("How long each card waits and flies. The deal stage waits on the same asset, so the table never moves on with cards still in the air.")]
		[Required]
		[SerializeField] private PokerDealPacing _pacing;

		[SerializeField] private Ease _ease = Ease.OutCubic;

		[Tooltip("How high the card rises on its way, along the table's up.")]
		[SerializeField] private float _arc = 0.08f;

		public override float DelayFor(PokerDealTurn turn) => _pacing ? _pacing.DelayFor(turn.Slot, turn.Order, turn.Players) : 0f;

		public override Tween Travel(Transform card, Transform parent, Vector3 localPosition, Quaternion localRotation)
			=> PokerCardVisual.ArcTween(card, parent, localPosition, localRotation, _pacing ? _pacing.CardFlight : 0.01f, _ease, _arc);
	}
}
