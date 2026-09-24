using DG.Tweening;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Visual
{
	// Where one card falls in the deal, worked out by the deck from the replicated seats so every client
	// agrees on it. Order counts round the table from the first seat, among the players being dealt in only.
	public readonly struct PokerDealTurn
	{
		public readonly int Slot;
		public readonly int Order;
		public readonly int Players;
		public readonly bool IntoHand;

		public PokerDealTurn(int slot, int order, int players, bool intoHand = false)
		{
			Slot = slot;
			Order = order;
			Players = Mathf.Max(1, players);
			IntoHand = intoHand;
		}
	}

	// How the deck deals: how long each card waits on it, and how it travels from there to its place. The
	// deck owns where it is and whose turn it is; this owns what the deal looks like, so a different deal
	// is a different subclass on the Deck object and nothing else changes.
	public abstract class PokerDealController : MonoBehaviour
	{
		// Seconds this card lies on the deck before it leaves.
		public abstract float DelayFor(PokerDealTurn turn);

		// Carries the card from where it lies now to its slot in `parent`'s space. The card is not parented
		// there until it lands — it holds it back for the wait, puts it exactly on the slot and only then
		// takes the new parent, so a subclass handles none of that.
		public abstract Tween Travel(Transform card, Transform parent, Vector3 localPosition, Quaternion localRotation, Vector3 localScale);
	}
}
