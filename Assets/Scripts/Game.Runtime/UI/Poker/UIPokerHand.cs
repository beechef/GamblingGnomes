using System.Collections.Generic;
using Game.Runtime.GameMode.Poker;
using Game.Runtime.GameMode.Poker.Player;
using Game.Runtime.GameMode.Poker.Visual;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Runtime.UI.Poker
{
	// The local player's own two cards, read straight off their PokerPlayerData — the hole card list
	// is owner-read, so this UI is only ever able to draw the hand it belongs to.
	public class UIPokerHand : UIPokerView
	{
		[Header("References")]
		[SerializeField] private PokerCardDatabase _database;
		[SerializeField] private Image _cardImagePrefab;
		[SerializeField] private RectTransform _cardContainer;

		private readonly List<Image> _cardImages = new();

		private PokerPlayerData _boundData;

		protected override void OnTick()
		{
			var localPlayer = GameMode.FindSeatedPlayer(LocalClientId);
			var data = localPlayer ? localPlayer.Data : null;

			if (data == _boundData) return;

			if (_boundData) _boundData.OnHoleCardsChanged -= HandleHoleCardsChanged;

			_boundData = data;

			if (_boundData) _boundData.OnHoleCardsChanged += HandleHoleCardsChanged;

			Refresh();
		}

		protected override void OnUnbind()
		{
			if (_boundData) _boundData.OnHoleCardsChanged -= HandleHoleCardsChanged;

			_boundData = null;
			Refresh();
		}

		// Two flat images with no animation to lose, so the screen copy just redraws.
		private void HandleHoleCardsChanged(NetworkListEvent<CardData> change) => Refresh();

		private void Refresh()
		{
			if (!_cardImagePrefab || !_cardContainer || !_database) return;

			var count = _boundData ? _boundData.HoleCards.Count : 0;

			while (_cardImages.Count < count)
			{
				_cardImages.Add(Instantiate(_cardImagePrefab, _cardContainer));
			}

			for (var i = 0; i < _cardImages.Count; i++)
			{
				var image = _cardImages[i];
				var active = i < count;
				image.gameObject.SetActive(active);

				if (active) image.sprite = _database.GetFace(_boundData.HoleCards[i]);
			}
		}
	}
}
