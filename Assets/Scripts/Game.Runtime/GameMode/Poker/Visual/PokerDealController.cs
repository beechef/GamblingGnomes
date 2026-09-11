using DG.Tweening;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Visual
{
	// Where one card falls in the deal, worked out by the deck from the replicated seats so every client
	// agrees on it. Order counts round the table from the seat after the dealer, among the players being
	// dealt in only.
	public readonly struct PokerDealTurn
	{
		public readonly int Slot;
		public readonly int Order;
		public readonly int Players;

		public PokerDealTurn(int slot, int order, int players)
		{
			Slot = slot;
			Order = order;
			Players = Mathf.Max(1, players);
		}
	}

	// How the deck deals: how long each card waits on it, and how it travels from there to its place. The
	// deck owns where it is and whose turn it is; this owns what the deal looks like, so a different deal
	// is a different subclass on the Deck object and nothing else changes.
	public abstract class PokerDealController : MonoBehaviour
	{
		// Seconds this card lies on the deck before it leaves.
		public abstract float DelayFor(PokerDealTurn turn);

		// Carries the card from where it lies now to its slot, both in its parent's space. The card has
		// already been parented to where it is going. The card holds it back for the wait and puts it
		// exactly on the slot when it ends, so a subclass handles neither.
		public abstract Tween Travel(Transform card, Vector3 localPosition, Quaternion localRotation);
	}
}
