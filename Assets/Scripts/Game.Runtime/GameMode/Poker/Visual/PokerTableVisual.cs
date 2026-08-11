using System.Collections.Generic;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Visual
{
	public class PokerTableVisual : MonoBehaviour
	{
		[Header("Community Cards")]
		[SerializeField] private PokerCardVisual _cardPrefab;
		[SerializeField] private PokerCardDatabase _database;
		[SerializeField] private Transform _communityRoot;
		[SerializeField] private float _cardSpacing = 0.06f;

		private readonly List<PokerCardVisual> _spawnedCards = new();

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

			_gameMode.Data.OnCommunityCardsChanged += RefreshCommunityCards;
			RefreshCommunityCards();
		}

		private void Unbind()
		{
			if (_gameMode && _gameMode.Data) _gameMode.Data.OnCommunityCardsChanged -= RefreshCommunityCards;

			_gameMode = null;
		}

		private void RefreshCommunityCards()
		{
			if (!_gameMode || !_gameMode.Data || !_cardPrefab) return;

			var cards = _gameMode.Data.CommunityCards;

			while (_spawnedCards.Count < cards.Count)
			{
				var card = Instantiate(_cardPrefab, _communityRoot ? _communityRoot : transform);
				_spawnedCards.Add(card);
			}

			for (var i = 0; i < _spawnedCards.Count; i++)
			{
				var visual = _spawnedCards[i];
				var active = i < cards.Count;
				visual.gameObject.SetActive(active);

				if (!active) continue;

				// Laid out around the centre of the root so the board grows outwards as streets open.
				var offset = (i - (cards.Count - 1) * 0.5f) * _cardSpacing;
				visual.transform.localPosition = new Vector3(offset, 0f, 0f);
				visual.transform.localRotation = Quaternion.identity;
				visual.SetCard(cards[i], true, _database);
			}
		}
	}
}
