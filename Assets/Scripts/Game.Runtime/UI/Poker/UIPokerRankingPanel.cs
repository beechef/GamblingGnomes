using System.Collections.Generic;
using Game.Runtime.GameMode.Poker.Player;
using UnityEngine;

namespace Game.Runtime.UI.Poker
{
	// The board that goes up when the hand is settled. It mirrors the table's showdown list rather than
	// working the ranking out for itself — the server already decided who placed where, and two clients
	// disagreeing about that would be worse than a frame of lag.
	public class UIPokerRankingPanel : UIPokerView
	{
		[Header("Panel")]
		[SerializeField] private GameObject _panel;

		[Header("Rows")]
		[Tooltip("One per finishing place, spawned as the board is filled — a table of two and a table of eight both fit.")]
		[SerializeField] private UIPokerRankingRow _rowPrefab;

		[Tooltip("Laid out by its own layout group, so this only has to add and remove children.")]
		[SerializeField] private RectTransform _rowContainer;

		[Tooltip("Board cards are sized here for the same reason the row sizes its own: a stretched card stops looking like one.")]
		[SerializeField] private Vector2 _communityCardSize = new(80f, 120f);

		[Header("Community")]
		[SerializeField] private RectTransform _communityContainer;
		[SerializeField] private UIPokerCard _cardPrefab;

		private readonly List<UIPokerCard> _communityCards = new();
		private readonly List<UIPokerRankingRow> _rows = new();

		private void Awake()
		{
			if (_panel) _panel.SetActive(false);
		}

		protected override void OnBind()
		{
			Data.OnShowdownChanged += Refresh;
			Refresh();
		}

		protected override void OnUnbind()
		{
			Data.OnShowdownChanged -= Refresh;

			if (_panel) _panel.SetActive(false);
		}

		private void Refresh()
		{
			var showdown = Data.Showdown;
			var visible = showdown.Count > 0;

			if (_panel && _panel.activeSelf != visible) _panel.SetActive(visible);
			if (!visible) return;

			RefreshRows(showdown.Count);
			RefreshCommunity();
		}

		private void RefreshRows(int count)
		{
			if (!_rowPrefab || !_rowContainer) return;

			while (_rows.Count < count) _rows.Add(Instantiate(_rowPrefab, _rowContainer));

			for (var i = 0; i < _rows.Count; i++)
			{
				var used = i < count;
				if (_rows[i].gameObject.activeSelf != used) _rows[i].gameObject.SetActive(used);

				if (!used) continue;

				var entry = Data.Showdown[i];
				_rows[i].SetEntry(entry, PokerPlayer.Find(entry.ClientId));
			}
		}

		private void RefreshCommunity()
		{
			if (!_communityContainer || !_cardPrefab) return;

			var cards = Data.CommunityCards;

			while (_communityCards.Count < cards.Count)
			{
				var card = Instantiate(_cardPrefab, _communityContainer);
				((RectTransform)card.transform).sizeDelta = _communityCardSize;

				_communityCards.Add(card);
			}

			for (var i = 0; i < _communityCards.Count; i++)
			{
				var used = i < cards.Count;
				_communityCards[i].gameObject.SetActive(used);

				if (used) _communityCards[i].SetCard(cards[i]);
			}
		}
	}
}
