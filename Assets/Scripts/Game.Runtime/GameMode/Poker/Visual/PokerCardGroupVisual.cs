using System.Collections.Generic;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Visual
{
	// A place cards can be, and how they are arranged once they are there. A player has two — the spot in
	// front of their chair and the hand they are holding — and picking a card up is that card moving from
	// one group to the other, rather than one list with two branches in its layout.
	//
	// The arrangement is the whole difference between them: five cards lying on a table are a row spread
	// wide enough for a raycast to tell them apart, and the same five held up are a fan. So a group is a
	// subclass with its own anchor and its own slot maths, and a third arrangement is a third subclass
	// rather than another branch here.
	public abstract class PokerCardGroupVisual : MonoBehaviour
	{
		[Tooltip("Gap between cards along the anchor's forward. Coplanar cards z-fight.")]
		[SerializeField] private float _depthStep = 0.0008f;

		[Tooltip("Writes the slot each card ended up in, once per layout. The one thing that separates 'laid out wrong' from 'drawn in the wrong order', and the two need opposite fixes.")]
		[SerializeField] private bool _logLayout;

		private readonly List<PokerCardVisual> _cards = new();

		// Where each card came in the deal. A card is drawn where the hand it belongs to says, not where it
		// happened to arrive: picking the third card up first must not put it at the front of the fan.
		private readonly List<int> _order = new();

		public IReadOnlyList<PokerCardVisual> Cards => _cards;

		// The transform these cards hang off. Public because whoever spawns a card wants it to start out
		// here rather than travelling in from wherever the prefab landed.
		public Transform Anchor => ResolveAnchor();

		protected float DepthStep => _depthStep;

		// How big the cards being arranged are, asked of one of them. An arrangement that needs the number
		// takes it from the deck it is holding rather than carrying its own copy, so a card resized in its
		// prefab reshapes every layout on its own instead of leaving one of them quietly on the old size.
		// Zero while the group is empty, which is also the only time no arrangement needs it.
		protected Vector2 CardSize
		{
			get
			{
				foreach (var card in _cards)
				{
					if (card) return card.Size;
				}

				return Vector2.zero;
			}
		}

		public bool Contains(PokerCardVisual card) => card && _cards.Contains(card);

		// Taken in at its place in the deal. The card that has just arrived is the only one that travels:
		// the rest are closing a gap where they already lie, and an arc played on them says something
		// happened to cards nothing happened to.
		public void Add(PokerCardVisual card, int order, bool animate)
		{
			if (!card || Contains(card)) return;

			var at = _cards.Count;
			for (var i = 0; i < _order.Count; i++)
			{
				if (_order[i] <= order) continue;

				at = i;
				break;
			}

			_cards.Insert(at, card);
			_order.Insert(at, order);

			Layout(animate ? card : null);
		}

		public void Remove(PokerCardVisual card)
		{
			var index = _cards.IndexOf(card);
			if (index < 0) return;

			_cards.RemoveAt(index);
			_order.RemoveAt(index);

			Layout(null);
		}

		public void Clear()
		{
			_cards.Clear();
			_order.Clear();
		}

		// All of them, because one card arriving or leaving changes where every other one belongs.
		public void Layout(PokerCardVisual animated)
		{
			var anchor = ResolveAnchor();

			for (var i = 0; i < _cards.Count; i++)
			{
				if (!_cards[i]) continue;

				_cards[i].PlaceAt(anchor, SlotPosition(i, _cards.Count), SlotRotation(i, _cards.Count),
					_cards[i] == animated);
			}

			if (_logLayout) LogLayout();
		}

		// Once per layout rather than once per frame, and it prints what the slot maths was actually given:
		// the deal order each card came in with, in the order this group put them. A hand that looks wrong
		// is either laid out wrong or drawn in the wrong order, and nothing on screen tells the two apart —
		// this does, in one line.
		private void LogLayout()
		{
			var line = name + " laid out " + _cards.Count + ":";

			for (var i = 0; i < _cards.Count; i++)
			{
				line += "\n  slot " + i + "  deal order " + _order[i]
					+ "  card " + (_cards[i] ? _cards[i].Card.ToString() : "none")
					+ "  local " + SlotPosition(i, _cards.Count).ToString("F4");
			}

			Debug.Log(line, this);
		}

		protected abstract Transform ResolveAnchor();

		protected abstract Vector3 SlotPosition(int slot, int count);

		protected virtual Quaternion SlotRotation(int slot, int count) => Quaternion.identity;
	}
}
