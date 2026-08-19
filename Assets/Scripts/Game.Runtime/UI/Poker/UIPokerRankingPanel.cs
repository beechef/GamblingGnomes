using System.Collections.Generic;
using Game.Runtime.GameMode.Poker;
using Game.Runtime.GameMode.Poker.Player;
using Unity.Netcode;
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

		[Tooltip("How far the outermost board card leans, in degrees. The row fans evenly between the two edges so the board reads as cards laid down by hand rather than a row of tiles. Zero lays them flat; a negative angle fans the other way.")]
		[Range(-30f, 30f)]
		[SerializeField] private float _communityFanAngle = 5f;

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

			// The board and what may be seen of it are separate values, and the showdown list arriving
			// first is the ordinary case rather than the odd one — a panel that only listened for the list
			// would draw the board exactly once, at the moment it knew least about it.
			Data.OnCommunityCardsChanged += HandleCommunityCardsChanged;
			Data.OnCommunityVisibilityChanged += Refresh;

			Refresh();
		}

		protected override void OnUnbind()
		{
			Data.OnCommunityVisibilityChanged -= Refresh;
			Data.OnCommunityCardsChanged -= HandleCommunityCardsChanged;
			Data.OnShowdownChanged -= Refresh;

			if (_panel) _panel.SetActive(false);
		}

		private void HandleCommunityCardsChanged(NetworkListEvent<CardData> change) => Refresh();

		// Spread evenly between the two edges, so the middle of any board sits upright and a board of one
		// card is not left leaning on nothing.
		private float FanAngle(int index, int count)
		{
			if (count <= 1) return 0f;

			return Mathf.Lerp(_communityFanAngle, -_communityFanAngle, index / (float)(count - 1));
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

				if (!used) continue;

				// Re-aimed on every pass rather than once at creation: the lean is a card's place in the
				// fan, and a board that grows from three to five moves every card's place with it.
				_communityCards[i].transform.localRotation = Quaternion.Euler(0f, 0f, FanAngle(i, cards.Count));

				// The board is on the table whole from the deal, so a hand that ended before the river shows
				// the cards it never turned as backs rather than spoiling them here.
				_communityCards[i].SetCard(cards[i], Data.IsCommunityCardVisible(i));
			}
		}
	}
}
