using System.Collections.Generic;
using Game.Runtime.GameMode.Poker;
using Game.Runtime.GameMode.Poker.Visual;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Runtime.UI.Poker
{
	// The local player's own cards, read straight off the player this client owns.
	public class UIPokerHand : UIPokerView
	{
		[Header("References")]
		[SerializeField] private PokerCardDatabase _database;
		[SerializeField] private Image _cardImagePrefab;
		[SerializeField] private RectTransform _cardContainer;

		private readonly List<Image> _cardImages = new();

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
			if (!_cardImagePrefab || !_cardContainer || !_database) return;

			var cards = LocalData.HoleCards;

			while (_cardImages.Count < cards.Count) _cardImages.Add(Instantiate(_cardImagePrefab, _cardContainer));

			for (var i = 0; i < _cardImages.Count; i++)
			{
				var image = _cardImages[i];
				var active = i < cards.Count;
				image.gameObject.SetActive(active);

				if (active) image.sprite = _database.GetFace(cards[i]);
			}
		}

		private void HideAll()
		{
			foreach (var image in _cardImages)
			{
				if (image) image.gameObject.SetActive(false);
			}
		}
	}
}
