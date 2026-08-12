using System.Collections.Generic;
using Game.Runtime.GameMode.Poker;
using Unity.Netcode;
using UnityEngine;

namespace Game.Runtime.UI.Poker
{
	// The local player's own cards, read straight off the player this client owns.
	public class UIPokerHand : UIPokerView
	{
		[Header("References")]
		[SerializeField] private UIPokerCard _cardPrefab;
		[SerializeField] private RectTransform _cardContainer;

		private readonly List<UIPokerCard> _cards = new();

		protected override void OnBind()
		{
			LocalData.OnHoleCardsChanged += HandleHoleCardsChanged;
			Refresh();
		}

		protected override void OnUnbind()
		{
			LocalData.OnHoleCardsChanged -= HandleHoleCardsChanged;
			HideAll();
		}

		// Two flat images with no animation to lose, so the screen copy just redraws.
		private void HandleHoleCardsChanged(NetworkListEvent<CardData> change) => Refresh();

		private void Refresh()
		{
			if (!_cardPrefab || !_cardContainer) return;

			var cards = LocalData.HoleCards;

			while (_cards.Count < cards.Count) _cards.Add(Instantiate(_cardPrefab, _cardContainer));

			for (var i = 0; i < _cards.Count; i++)
			{
				var active = i < cards.Count;
				_cards[i].gameObject.SetActive(active);

				if (active) _cards[i].SetCard(cards[i]);
			}
		}

		private void HideAll()
		{
			foreach (var card in _cards)
			{
				if (card) card.gameObject.SetActive(false);
			}
		}
	}
}
