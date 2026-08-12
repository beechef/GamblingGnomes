using System.Collections.Generic;
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

		private readonly List<PokerCardVisual> _cards = new();

		private PokerGameMode _gameMode;

		private void Update()
		{
			var gameMode = PokerGameMode.Instance;
			if (gameMode == _gameMode) return;

			Unbind();
			_gameMode = gameMode;
			Bind();
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
				// stacked towards the face side so the separation lifts cards off the table.
				var offset = (i - (_cards.Count - 1) * 0.5f) * _cardSpacing;
				visual.transform.localPosition = new Vector3(offset, 0f, -i * _depthStep);
				visual.transform.localRotation = Quaternion.identity;
			}
		}
	}
}
