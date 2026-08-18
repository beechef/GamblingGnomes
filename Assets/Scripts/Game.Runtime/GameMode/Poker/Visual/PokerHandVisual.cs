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
	[RequireComponent(typeof(PokerPlayerData))]
	public class PokerHandVisual : NetworkBehaviour
	{
		[Header("Layout")]
		[SerializeField] private float _cardSpacing = 0.03f;
		[SerializeField] private float _fanAngle = 8f;

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
		private bool _shownFaceUp;

		public override void OnNetworkSpawn()
		{
			if (!_data) _data = GetComponent<PokerPlayerData>();
			if (!_rig) _rig = GetComponent<PlayerRigController>();
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

		// Visibility is a property of the hand, not of any one card: a showdown turns over what is
		// already being held.
		private void HandleStateChanged()
		{
			if (!_data || _data.IsHandVisible == _shownFaceUp) return;

			_shownFaceUp = _data.IsHandVisible;

			for (var i = 0; i < _cards.Count; i++)
			{
				if (_cards[i]) _cards[i].SetCard(CardAt(i), _shownFaceUp, _database, true);
			}
		}

		private CardData CardAt(int index)
		{
			if (!_data || index < 0 || index >= _data.HoleCards.Count) return CardData.None;

			return _data.IsHandVisible ? _data.HoleCards[index] : CardData.None;
		}

		private void AddCard(CardData card, bool animate)
		{
			if (!_cardPrefab) return;

			var visual = Instantiate(_cardPrefab, ResolveAnchor());
			_cards.Add(visual);

			_shownFaceUp = _data && _data.IsHandVisible;
			visual.SetCard(_shownFaceUp ? card : CardData.None, _shownFaceUp, _database, animate);
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
		}

		private void UpdateCard(int index, CardData card)
		{
			if (index < 0 || index >= _cards.Count) return;

			var visible = _data && _data.IsHandVisible;
			_cards[index].SetCard(visible ? card : CardData.None, visible, _database);
		}

		private void RebuildAll()
		{
			ClearCards();

			if (!_data) return;

			_shownFaceUp = _data.IsHandVisible;

			var count = _data.IsHandVisible ? _data.HoleCards.Count : _data.CardCount;
			for (var i = 0; i < count; i++) AddCard(CardAt(i), false);

			Layout();
		}

		private void Layout()
		{
			for (var i = 0; i < _cards.Count; i++)
			{
				var visual = _cards[i];
				if (!visual) continue;

				var offset = (i - (_cards.Count - 1) * 0.5f) * _cardSpacing;
				visual.transform.localPosition = new Vector3(offset, 0f, -i * _depthStep);
				visual.transform.localRotation = Quaternion.Euler(0f, 0f, -offset / Mathf.Max(_cardSpacing, 0.0001f) * _fanAngle);
			}
		}

		// The owner renders the hand-only rig and everyone else renders the full body, so the cards hang
		// off whichever right hand this client is actually drawing — which rig that is stays the rig's
		// business, not this view's. With no anchor assigned a holder is parented to the bone, which keeps
		// the cards in the hand as it animates.
		// Built once under the hand of whichever rig this client renders. There is no serialized anchor to
		// override it with: an anchor authored in the prefab would name a bone on one rig and be wrong on
		// the other, which is why both fields it used to offer sat empty in every prefab that had them.
		private Transform ResolveAnchor()
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
