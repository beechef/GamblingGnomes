using System.Collections.Generic;
using Game.Runtime.Controller;
using Unity.Netcode;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Visual
{
	// The board is built up card by card, the way it is dealt. A full rebuild only happens when this
	// client arrives to a table that is already running, because that is the only time it has nothing
	// to preserve — every other path would restart the animations of cards already lying face up.
	public class PokerTableVisual : MonoBehaviour
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

		private PokerGameMode _gameMode;
		private Transform _viewer;

		private void Update()
		{
			var gameMode = PokerGameMode.Instance;
			if (gameMode != _gameMode)
			{
				Unbind();
				_gameMode = gameMode;
				Bind();
			}

			FaceViewer();
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

		private void OnDisable() => Unbind();

		private void Bind()
		{
			if (!_gameMode || !_gameMode.Data) return;

			_gameMode.Data.OnCommunityCardsChanged += HandleCommunityCardsChanged;

			// Late join: replicate the board as it stands, with no flips to replay.
			RebuildAll();
		}

		private void Unbind()
		{
			if (_gameMode && _gameMode.Data) _gameMode.Data.OnCommunityCardsChanged -= HandleCommunityCardsChanged;

			_gameMode = null;
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
			visual.SetCard(card, true, _database, animate);
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

			_cards[index].SetCard(card, true, _database);
		}

		private void RebuildAll()
		{
			ClearCards();

			if (!_gameMode || !_gameMode.Data) return;

			foreach (var card in _gameMode.Data.CommunityCards) AddCard(card, false);

			Layout();
		}

		private void Layout()
		{
			for (var i = 0; i < _cards.Count; i++)
			{
				var visual = _cards[i];
				if (!visual) continue;

				// Laid out around the centre of the root so the board grows outwards as streets open, and
				// stacked towards the face side so the separation lifts cards off the table. Spacing
				// follows the scale, or larger cards would overlap.
				var offset = (i - (_cards.Count - 1) * 0.5f) * _cardSpacing * _cardScale;
				visual.transform.localPosition = new Vector3(offset, 0f, -i * _depthStep);
				visual.transform.localRotation = Quaternion.identity;
			}
		}
	}
}
