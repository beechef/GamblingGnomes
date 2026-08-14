using System.Collections.Generic;
using Game.Runtime.Controller;
using Unity.Netcode;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Visual
{
	// The board mirrors the table's list card for card: the whole board goes down at the deal, face down,
	// and a street turns over the ones it opens where they already lie. Which cards are face up is the
	// table's rule to answer, not this view's to work out — a street opening and an ability reading the
	// river reach it the same way. A full rebuild only happens when this client arrives to a table already
	// running, because that is the only time it has nothing to preserve; every other path would restart
	// the flips of cards already lying face up.
	public class PokerTableVisual : PokerVisual
	{
		[Header("Community Cards")]
		[SerializeField] private PokerCardVisual _cardPrefab;
		[SerializeField] private PokerCardDatabase _database;
		[SerializeField] private Transform _communityRoot;
		[SerializeField] private float _cardSpacing = 0.06f;

		[Tooltip("Gap between cards along the root's forward. Coplanar cards z-fight.")]
		[SerializeField] private float _depthStep = 0.0008f;

		[Header("Presentation")]
		[Tooltip("The board is read from across the table, so it is drawn larger than the cards in hand.")]
		[SerializeField] private float _cardScale = 2.6f;

		[Tooltip("Turns the board so its cards read the right way up for whoever is looking at them. Local only — each client turns its own copy.")]
		[SerializeField] private bool _faceLocalViewer = true;

		[SerializeField] private float _faceTurnSpeed = 180f;

		private readonly List<PokerCardVisual> _cards = new();

		private Transform _viewer;

		private void Update() => FaceViewer();

		protected override void OnBind()
		{
			Data.OnCommunityCardsChanged += HandleCommunityCardsChanged;
			Data.OnCommunityVisibilityChanged += RefreshVisibility;

			// Late join: replicate the board as it stands, with no flips to replay.
			RebuildAll();
		}

		protected override void OnUnbind()
		{
			if (Data)
			{
				Data.OnCommunityVisibilityChanged -= RefreshVisibility;
				Data.OnCommunityCardsChanged -= HandleCommunityCardsChanged;
			}

			// The table is gone, so a board with nothing left to mirror does not linger.
			ClearCards();
		}

		// A card lying flat only reads from one side of the table, so the board turns to whoever is
		// looking: its top edge points away from the camera, the way a card dealt to you would sit.
		private void FaceViewer()
		{
			if (!_faceLocalViewer || !_communityRoot) return;

			var viewer = ResolveViewer();
			if (!viewer) return;

			var away = _communityRoot.position - viewer.position;
			away.y = 0f;
			if (away.sqrMagnitude < 0.0001f) return;

			// Down as forward keeps the faces pointing at the ceiling while the up axis does the turning.
			var target = Quaternion.LookRotation(Vector3.down, away.normalized);
			_communityRoot.rotation = Quaternion.RotateTowards(_communityRoot.rotation, target, _faceTurnSpeed * Time.deltaTime);
		}

		// Left null until the bootstrap camera announces itself, and retried each frame after — cheaper
		// and far more certain than guessing at whatever camera a scene scan turns up.
		private Transform ResolveViewer()
		{
			if (_viewer) return _viewer;

			return _viewer = GameCamera.View;
		}

		private void HandleCommunityCardsChanged(NetworkListEvent<CardData> change)
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

		private void AddCard(CardData card, bool animate)
		{
			if (!_cardPrefab) return;

			var visual = Instantiate(_cardPrefab, _communityRoot ? _communityRoot : transform);
			visual.transform.localScale = Vector3.one * _cardScale;

			_cards.Add(visual);

			// The index it lands on is the one the table answers about, so a card added to a board that is
			// already half open arrives showing the right face.
			visual.SetCard(card, IsFaceUp(_cards.Count - 1), _database, animate);
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

			_cards[index].SetCard(card, IsFaceUp(index), _database);
		}

		// The path a street actually takes: nothing about the cards changed, only how much of the board may
		// be seen. Animated, because this is the flip.
		private void RefreshVisibility()
		{
			for (var i = 0; i < _cards.Count; i++)
			{
				if (!_cards[i]) continue;

				var faceUp = IsFaceUp(i);
				if (_cards[i].FaceUp == faceUp) continue;

				_cards[i].SetCard(CardAt(i), faceUp, _database, true);
			}
		}

		private bool IsFaceUp(int index) => Data && Data.IsCommunityCardVisible(index);

		private CardData CardAt(int index)
		{
			if (!Data || index < 0 || index >= Data.CommunityCards.Count) return CardData.None;

			return Data.CommunityCards[index];
		}

		private void RebuildAll()
		{
			ClearCards();

			if (!Data) return;

			foreach (var card in Data.CommunityCards) AddCard(card, false);

			Layout();
		}

		private void Layout()
		{
			for (var i = 0; i < _cards.Count; i++)
			{
				var visual = _cards[i];
				if (!visual) continue;

				// Laid out around the centre of the root, so a board that goes down whole sits centred from
				// the deal and no card slides as the streets turn it over. Stacked towards the face side so
				// the separation lifts cards off the table; spacing follows the scale, or larger cards
				// would overlap.
				var offset = (i - (_cards.Count - 1) * 0.5f) * _cardSpacing * _cardScale;
				visual.transform.localPosition = new Vector3(offset, 0f, -i * _depthStep);
				visual.transform.localRotation = Quaternion.identity;
			}
		}
	}
}
