using System;
using System.Collections.Generic;
using Game.Runtime.GameMode.Poker.Player;
using Sirenix.OdinInspector;
using Unity.Netcode;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Visual
{
	// Lives on the player so the cards travel with the gnome holding them. Cards are dealt in one at a
	// time and only re-read wholesale on a late join; a showdown flips the cards already in hand rather
	// than replacing them.
	//
	// It owns the slots — which card is in which, which faces are up, which of them a raycast may pick —
	// and nothing about where any of them is drawn. Where is a PokerCardGroupVisual's business: this one
	// only says which group a card belongs to right now, and hands it over when that answer changes.
	public class PokerHandVisual : NetworkBehaviour
	{
		[Header("Groups")]
		[Tooltip("Where a card lies before it is picked up. The row in front of the chair.")]
		[Required]
		[SerializeField] private PokerCardGroupVisual _table;

		[Tooltip("Where a card goes once it has been picked up. The fan in the hand.")]
		[Required]
		[SerializeField] private PokerCardGroupVisual _hand;

		[Header("References")]
		[SerializeField] private PokerPlayerData _data;
		[SerializeField] private PokerCardVisual _cardPrefab;
		[SerializeField] private PokerCardDatabase _database;

		private readonly List<PokerCardVisual> _cards = new();

		public IReadOnlyList<PokerCardVisual> Cards => _cards;

		// Cards are torn down and dealt again every round, so anything drawing on them has to be told
		// rather than resolving once. Static because the things that care are about every hand at the
		// table, not about one player's.
		public static event Action OnAnyHandChanged;

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void ResetStatics() => OnAnyHandChanged = null;

		// One bit per slot, so a hand held face down to its own holder can turn three of five over and
		// leave the rest as backs. A single flag could only ever say "the whole hand" — which is what
		// this was, and what a round dealing more cards than a player may look at breaks.
		private int _shownFaceUpMask;
		private int _shownInHandMask;

		public override void OnNetworkSpawn()
		{
			if (!_data) _data = GetComponentInParent<PokerPlayerData>();
			if (!_data) return;

			_data.OnHoleCardsChanged += HandleHoleCardsChanged;
			_data.OnHoleCardPresentationChanged += HandlePresentationChanged;

			// Late join: whatever is already in this hand, shown as it stands.
			RebuildAll();
		}

		public override void OnNetworkDespawn()
		{
			if (!_data) return;

			_data.OnHoleCardsChanged -= HandleHoleCardsChanged;
			_data.OnHoleCardPresentationChanged -= HandlePresentationChanged;
		}

		private void HandleHoleCardsChanged(NetworkListEvent<CardData> change)
		{
			switch (change.Type)
			{
				case NetworkListEvent<CardData>.EventType.Add:
					AddCard(change.Value, true);
					break;

				case NetworkListEvent<CardData>.EventType.RemoveAt:
				case NetworkListEvent<CardData>.EventType.Remove:
					RemoveCard(change.Index);
					break;

				case NetworkListEvent<CardData>.EventType.Clear:
					ClearCards();
					break;

				case NetworkListEvent<CardData>.EventType.Value:
					UpdateCard(change.Index, change.Value);
					break;

				default:
					RebuildAll();
					break;
			}

			OnAnyHandChanged?.Invoke();
		}

		// Asked per card: a showdown turns the whole hand over at once, and a holder allowed to look at
		// only three of five turns those three and leaves the others backs.
		//
		// Two masks, not one. Which cards are face up and which are up in the hand are different questions
		// with different answers — a hand revealed at a showdown goes back down on the table while staying
		// face up — and a guard built on the face alone left a picked-up card turned over but still lying
		// where it was, which is the whole pickup nobody could see happen.
		private void HandlePresentationChanged()
		{
			if (!_data) return;

			var faceUp = CurrentFaceUpMask();
			var inHand = CurrentInHandMask();
			if (faceUp == _shownFaceUpMask && inHand == _shownInHandMask) return;

			// Exactly the slots whose face changed. Redrawing the whole hand plays the flip on every card
			// in it, so turning one over made the other four flip along with it — a change-guard on the
			// hand answers "did anything change", and what has to be redrawn is "which one".
			var turned = faceUp ^ _shownFaceUpMask;

			// Exactly the slots that changed hands. Everything else is still where it was, and only the
			// group it sits in has to close the gap around it.
			var moved = inHand ^ _shownInHandMask;

			_shownFaceUpMask = faceUp;
			_shownInHandMask = inHand;

			for (var i = 0; i < _cards.Count && i < 31; i++)
			{
				if ((turned & (1 << i)) == 0) continue;

				var visible = IsVisible(i);
				if (_cards[i]) _cards[i].SetCard(visible ? CardAt(i) : CardData.None, visible, _database, true);
			}

			for (var i = 0; i < _cards.Count && i < 31; i++)
			{
				if ((moved & (1 << i)) == 0) continue;

				HandOver(i, true);
			}

			OnAnyHandChanged?.Invoke();
		}

		// Which group this slot belongs in now, and the move if it is not already there. Both groups lay
		// themselves out again as it leaves and arrives, so picking one card up closes the gap it left on
		// the table without anything here knowing how either of them is arranged.
		private void HandOver(int index, bool animate)
		{
			if (index < 0 || index >= _cards.Count) return;

			var card = _cards[index];
			if (!card) return;

			var target = IsInHand(index) ? _hand : _table;
			var previous = IsInHand(index) ? _table : _hand;
			if (!target || target.Contains(card)) return;

			if (previous) previous.Remove(card);

			target.Add(card, index, animate);
		}

		private int CurrentInHandMask()
		{
			var mask = 0;
			if (!_data) return mask;

			for (var i = 0; i < _cards.Count && i < 31; i++)
			{
				if (IsInHand(i)) mask |= 1 << i;
			}

			return mask;
		}

		private bool IsVisible(int index) => _data && _data.IsHoleCardVisible(index);

		// Over the cards that exist, not over the replicated list — the two are not the same length while a
		// hand is arriving, and they are a different length on a host than on a client. The server adds to
		// HoleCards one card at a time and each add raises its event there and then, so a host building
		// card 0 sees a list of one; a client is sent the whole list in a single delta and only then told
		// about each element, so it builds card 0 against a list of five. Counting the replicated list here
		// recorded bits for slots this view had not drawn yet, and a bit already set is a change that never
		// arrives — the card is then left wherever it was spawned, on whichever machine lost the race.
		private int CurrentFaceUpMask()
		{
			var mask = 0;
			if (!_data) return mask;

			for (var i = 0; i < _cards.Count && i < 31; i++)
			{
				if (IsVisible(i)) mask |= 1 << i;
			}

			return mask;
		}

		private CardData CardAt(int index)
		{
			if (!_data || index < 0 || index >= _data.HoleCards.Count) return CardData.None;

			return IsVisible(index) ? _data.HoleCards[index] : CardData.None;
		}

		private void AddCard(CardData card, bool animate)
		{
			if (!_cardPrefab) return;

			// The slot this card is about to occupy, so a hand whose cards are turned one at a time asks
			// about the right one.
			var index = _cards.Count;
			var group = IsInHand(index) ? _hand : _table;

			// Spawned under the group it belongs to, so a dealt card travels the short hop from that spot
			// to its slot rather than flying in from wherever the prefab happened to sit.
			var visual = Instantiate(_cardPrefab, group ? group.Anchor : transform);
			_cards.Add(visual);

			var visible = IsVisible(index);
			visual.SetCard(visible ? card : CardData.None, visible, _database, animate);

			_shownFaceUpMask = CurrentFaceUpMask();
			_shownInHandMask = CurrentInHandMask();

			HandOver(index, animate);
		}

		private void RemoveCard(int index)
		{
			if (index < 0 || index >= _cards.Count) return;

			var visual = _cards[index];
			_cards.RemoveAt(index);

			if (!visual) return;

			// Off the group before it is destroyed: Destroy is deferred, so a group left holding it would
			// lay out around a card that is on its way out.
			if (_table) _table.Remove(visual);
			if (_hand) _hand.Remove(visual);

			Destroy(visual.gameObject);
		}

		private void ClearCards()
		{
			if (_table) _table.Clear();
			if (_hand) _hand.Clear();

			foreach (var visual in _cards)
			{
				if (visual) Destroy(visual.gameObject);
			}

			_cards.Clear();

			// The masks describe what is drawn, and nothing is: leaving them set would make the first card of
			// the next hand look like no change at all.
			_shownFaceUpMask = 0;
			_shownInHandMask = 0;
		}

		private void UpdateCard(int index, CardData card)
		{
			if (index < 0 || index >= _cards.Count) return;

			var visible = IsVisible(index);
			_cards[index].SetCard(visible ? card : CardData.None, visible, _database);
		}

		private void RebuildAll()
		{
			ClearCards();

			if (!_data) return;

			// CardCount is HoleCards.Count: everyone knows how many cards are being held, whoever may
			// look at them.
			var count = _data.CardCount;
			for (var i = 0; i < count; i++) AddCard(CardAt(i), false);
		}

		private bool IsInHand(int index) => _data && _data.IsHoleCardInHand(index);

		// Opened by the beat that allows picking and closed when it ends. A card already up in the hand is
		// never offered: it has been taken, and reaching for it again is a pick the server would refuse.
		public void SetPickupable(bool pickupable)
		{
			for (var i = 0; i < _cards.Count; i++)
			{
				if (!_cards[i]) continue;

				_cards[i].Pickupable = pickupable && !IsInHand(i);
			}
		}

		// Which slot a card on screen belongs to. Asked by whatever a player just pointed at: the card
		// itself carries no index, and the list here is the only thing that knows the order they were
		// dealt in.
		public int SlotOf(PokerCardVisual visual)
		{
			for (var i = 0; i < _cards.Count; i++)
			{
				if (_cards[i] == visual) return i;
			}

			return -1;
		}
	}
}
