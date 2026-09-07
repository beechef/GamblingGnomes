using System.Collections.Generic;
using Game.Runtime.GameMode.Poker.Player;
using Game.Runtime.Player;
using Unity.Netcode;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Visual
{
	// Lives on the player so the cards travel with the gnome holding them. Cards are dealt in one at a
	// time and only re-read wholesale on a late join; a showdown flips the cards already in hand rather
	// than replacing them.
	public class PokerHandVisual : NetworkBehaviour
	{
		[Header("Layout")]
		[SerializeField] private float _cardSpacing = 0.03f;
		[SerializeField] private float _fanAngle = 8f;

		[Tooltip("Gap between the cards lying on the table. Wider than the fan: these are what a player reaches for, and two cards overlapping cannot be told apart by a raycast.")]
		[SerializeField] private float _tableSpacing = 0.06f;

		[Tooltip("Gap between cards along the anchor's forward. Coplanar cards z-fight.")]
		[SerializeField] private float _depthStep = 0.0008f;

		[Header("Hand Bone")]
		[Tooltip("The hand of whichever rig this client renders — the owner's own, or the body everyone else sees.")]
		[SerializeField] private PlayerBone _handBone = PlayerBone.HandLeft;

		[Tooltip("Where the cards sit in the hand, relative to that bone.")]
		[SerializeField] private Vector3 _handLocalPosition = new(0.02f, 0.01f, 0f);

		[SerializeField] private Vector3 _handLocalEuler = new(0f, 90f, 0f);

		[Header("References")]
		[SerializeField] private PokerPlayerData _data;
		[SerializeField] private PlayerRigController _rig;
		[SerializeField] private PokerCardVisual _cardPrefab;
		[SerializeField] private PokerCardDatabase _database;

		private readonly List<PokerCardVisual> _cards = new();

		private Transform _resolvedAnchor;
		// One bit per slot, so a hand held face down to its own holder can turn three of five over and
		// leave the rest as backs. A single flag could only ever say "the whole hand" — which is what
		// this was, and what a round dealing more cards than a player may look at breaks.
		private int _shownFaceUpMask;
		private int _shownInHandMask;

		public override void OnNetworkSpawn()
		{
			if (!_data) _data = GetComponentInParent<PokerPlayerData>();
			if (!_rig) _rig = GetComponentInParent<PlayerRigController>();
			if (!_data) return;

			_data.OnHoleCardsChanged += HandleHoleCardsChanged;
			_data.OnStateChanged += HandleStateChanged;

			// Late join: whatever is already in this hand, shown as it stands.
			RebuildAll();
		}

		public override void OnNetworkDespawn()
		{
			if (!_data) return;

			_data.OnHoleCardsChanged -= HandleHoleCardsChanged;
			_data.OnStateChanged -= HandleStateChanged;
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

			Layout();
		}

		// Asked per card: a showdown turns the whole hand over at once, and a holder allowed to look at
		// only three of five turns those three and leaves the others backs.
		//
		// Two masks, not one. Which cards are face up and which are up in the hand are different questions
		// with different answers — a hand revealed at a showdown goes back down on the table while staying
		// face up — and a guard built on the face alone left a picked-up card turned over but still lying
		// where it was, which is the whole pickup nobody could see happen.
		private void HandleStateChanged()
		{
			if (!_data) return;

			var faceUp = CurrentFaceUpMask();
			var inHand = CurrentInHandMask();
			if (faceUp == _shownFaceUpMask && inHand == _shownInHandMask) return;

			// Exactly the slots whose face changed. Redrawing the whole hand plays the flip on every card
			// in it, so turning one over made the other four flip along with it — a change-guard on the
			// hand answers "did anything change", and what has to be redrawn is "which one".
			var turned = faceUp ^ _shownFaceUpMask;

			_shownFaceUpMask = faceUp;
			_shownInHandMask = inHand;

			for (var i = 0; i < _cards.Count && i < 31; i++)
			{
				if ((turned & (1 << i)) == 0) continue;

				var visible = IsVisible(i);
				if (_cards[i]) _cards[i].SetCard(visible ? CardAt(i) : CardData.None, visible, _database, true);
			}

			// Lifted into the hand, or put back down on the table: either way the two groups have to be
			// laid out again among their own members, so picking one card up closes the gap it left.
			Layout();
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

		private int CurrentFaceUpMask()
		{
			var mask = 0;
			if (!_data) return mask;

			for (var i = 0; i < _data.HoleCards.Count && i < 31; i++)
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

			var visual = Instantiate(_cardPrefab, ResolveTableAnchor());
			_cards.Add(visual);

			var visible = IsVisible(index);
			visual.SetCard(visible ? card : CardData.None, visible, _database, animate);
			_shownFaceUpMask = CurrentFaceUpMask();
			_shownInHandMask = CurrentInHandMask();
		}

		private void RemoveCard(int index)
		{
			if (index < 0 || index >= _cards.Count) return;

			var visual = _cards[index];
			_cards.RemoveAt(index);
			if (visual) Destroy(visual.gameObject);
		}

		private void ClearCards()
		{
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

			Layout();
		}

		// Two places a card can be: lying face down in front of its owner, or up in their hand. Each
		// group is laid out among its own members, so picking one card up closes the gap on the table
		// rather than leaving a hole where it was.
		private void Layout(bool animate = true)
		{
			var handAnchor = ResolveHandAnchor();
			var tableAnchor = ResolveTableAnchor();

			var inHand = 0;
			var onTable = 0;
			for (var i = 0; i < _cards.Count; i++)
			{
				if (IsInHand(i)) inHand++;
				else onTable++;
			}

			var handSlot = 0;
			var tableSlot = 0;

			for (var i = 0; i < _cards.Count; i++)
			{
				var visual = _cards[i];
				if (!visual) continue;

				if (IsInHand(i))
				{
					visual.PlaceAt(handAnchor, FanPosition(handSlot, inHand), FanRotation(handSlot, inHand), animate);
					handSlot++;
					continue;
				}

				visual.PlaceAt(tableAnchor, RowPosition(tableSlot, onTable), Quaternion.identity, animate);
				tableSlot++;
			}
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

		private Vector3 FanPosition(int slot, int count)
		{
			var offset = (slot - (count - 1) * 0.5f) * _cardSpacing;
			return new Vector3(offset, 0f, -slot * _depthStep);
		}

		private Quaternion FanRotation(int slot, int count)
		{
			var offset = (slot - (count - 1) * 0.5f) * _cardSpacing;
			return Quaternion.Euler(0f, 0f, -offset / Mathf.Max(_cardSpacing, 0.0001f) * _fanAngle);
		}

		// Flat on the table, spread wider than a hand is: these are what a player reaches for, and two
		// cards overlapping by a fan's margin are two cards a raycast cannot tell apart.
		private Vector3 RowPosition(int slot, int count)
		{
			var offset = (slot - (count - 1) * 0.5f) * _tableSpacing;
			// Negative, like the fan and like everything else that lifts a card: a sprite is read from its
			// own -Z, so that is the side the table anchor points at the ceiling and the direction anything
			// coming off the table has to travel. A card dealt later rests on the ones already there.
			return new Vector3(offset, 0f, -slot * _depthStep);
		}

		// The seat this player is in owns where their cards lie: it is authored in the chair prefab, so
		// retuning it reaches every seat at once. With no seat — a body that is not at the table — the
		// hand is the only place left to put them.
		private Transform ResolveTableAnchor()
		{
			if (!_data) return ResolveHandAnchor();

			var mode = PokerGameMode.Instance;
			if (!mode) return ResolveHandAnchor();

			foreach (var seat in mode.Seats)
			{
				if (seat && seat.SeatIndex == _data.SeatIndex.Value) return seat.CardAnchor;
			}

			return ResolveHandAnchor();
		}
		// The owner renders the hand-only rig and everyone else renders the full body, so the cards hang
		// off whichever right hand this client is actually drawing — which rig that is stays the rig's
		// business, not this view's. With no anchor assigned a holder is parented to the bone, which keeps
		// the cards in the hand as it animates.
		// Built once under the hand of whichever rig this client renders. There is no serialized anchor to
		// override it with: an anchor authored in the prefab would name a bone on one rig and be wrong on
		// the other, which is why both fields it used to offer sat empty in every prefab that had them.
		private Transform ResolveHandAnchor()
		{
			if (_resolvedAnchor) return _resolvedAnchor;

			var bone = _rig ? _rig.GetBone(_handBone) : null;
			if (!bone) return _resolvedAnchor = transform;

			var holder = new GameObject("PokerHandAnchor").transform;
			holder.SetParent(bone, false);
			holder.localPosition = _handLocalPosition;
			holder.localRotation = Quaternion.Euler(_handLocalEuler);

			return _resolvedAnchor = holder;
		}
	}
}
