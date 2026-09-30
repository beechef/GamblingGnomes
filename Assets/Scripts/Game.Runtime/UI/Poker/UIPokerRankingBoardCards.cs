using System.Collections.Generic;
using Game.Runtime.GameMode.Poker;
using UnityEngine;

namespace Game.Runtime.UI.Poker
{
	// The community cards across the top of the showdown board, for a table that deals them. Like every place
	// on the board it keeps its own copy of what the hand ended with: the next deal clears the board it reads,
	// so a copy refreshed only while the board still has cards cannot be emptied while it is on screen.
	public class UIPokerRankingBoardCards : MonoBehaviour
	{
		[SerializeField] private RectTransform _container;

		[SerializeField] private UIPokerCard _cardPrefab;

		[Tooltip("Cards are sized here rather than by the layout group, so a card keeps its shape.")]
		[SerializeField] private Vector2 _cardSize = new(112f, 147f);

		private readonly List<UIPokerCard> _cards = new();
		private readonly List<CardData> _snapshot = new();
		private readonly List<bool> _revealed = new();

		// Face up only where the table turned it: the showdown scores only turned cards, and one the host alone
		// was shown is not the table's.
		public void Draw(PokerGameData data)
		{
			if (!data || !_container || !_cardPrefab) return;

			if (data.CommunityCards.Count > 0)
			{
				_snapshot.Clear();
				_revealed.Clear();

				for (var i = 0; i < data.CommunityCards.Count; i++)
				{
					_snapshot.Add(data.CommunityCards[i]);
					_revealed.Add(data.IsCommunityCardRevealed(i));
				}
			}

			while (_cards.Count < _snapshot.Count)
			{
				var card = Instantiate(_cardPrefab, _container);
				((RectTransform)card.transform).sizeDelta = _cardSize;
				_cards.Add(card);
			}

			for (var i = 0; i < _cards.Count; i++)
			{
				var used = i < _snapshot.Count;
				if (_cards[i].gameObject.activeSelf != used) _cards[i].gameObject.SetActive(used);
				if (used) _cards[i].SetCard(_snapshot[i], _revealed[i]);
			}
		}
	}
}
