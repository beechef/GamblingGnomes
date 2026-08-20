using System.Collections.Generic;
using Game.Runtime.GameMode.Poker;
using Game.Runtime.GameMode.Poker.Player;
using Game.Runtime.UI.Button;
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

		[Tooltip("Waves the board away for this hand only. Local and cosmetic: the ranking is the ceremony between hands, not something the table is waiting on an answer to.")]
		[SerializeField] private UIButton _closeButton;

		[Header("Rows")]
		[Tooltip("One per finishing place, spawned as the board is filled — a table of two and a table of eight both fit.")]
		[SerializeField] private UIPokerRankingRow _rowPrefab;

		[Tooltip("Laid out by its own layout group, so this only has to add and remove children.")]
		[SerializeField] private RectTransform _rowContainer;

		[Tooltip("Board cards are sized here for the same reason the row sizes its own: a stretched card stops looking like one.")]
		[SerializeField] private Vector2 _communityCardSize = new(80f, 120f);

		[Tooltip("Gap between board cards, measured across the row. Their lean comes from the fan below, so this is the only thing setting how far apart they sit.")]
		[SerializeField] private float _communityCardSpacing = 108f;

		[Tooltip("How far below the row the fan is held, in pixels — where a hand would be gripping it. The cards sit on a circle of this radius, so a smaller number opens the fan wider and drops its outer cards further. Zero or less lays them flat in a straight row.")]
		[SerializeField] private float _communityFanRadius = 1400f;

		[Header("Community")]
		[SerializeField] private RectTransform _communityContainer;
		[SerializeField] private UIPokerCard _cardPrefab;

		private readonly List<UIPokerCard> _communityCards = new();
		private readonly List<UIPokerRankingRow> _rows = new();

		private bool _dismissed;

		private void Awake()
		{
			if (_panel) _panel.SetActive(false);
		}

		protected override void OnBind()
		{
			if (_closeButton) _closeButton.OnClick += HandleClose;

			// Never carried between binds: a HUD rebuilt for a new match must not open holding the last
			// one's dismissal, which would read as a ranking that simply never came up.
			_dismissed = false;

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

			if (_closeButton) _closeButton.OnClick -= HandleClose;

			if (_panel) _panel.SetActive(false);
		}

		private void HandleCommunityCardsChanged(NetworkListEvent<CardData> change) => Refresh();

		// A fan is held somewhere below the cards; it is not each card spun about its own middle. Turning a
		// card in place splays its top and its bottom in opposite directions, which reads as a row of
		// skewed tiles rather than as a hand — the giveaway is the gaps opening along the bottom edge.
		//
		// The cards sit on a circle centred that far below the row instead, so every one of them leans away
		// from the same point, the outer ones ride lower than the middle, and the whole thing comes out of
		// a single number anybody can picture: how far down the hand holding it would be.
		//
		// Placed by hand rather than by a layout group, which owns position and would put them straight
		// back in a line. The count is small, fixed by the rules, and entirely this panel's business.
		private void PlaceOnFan(RectTransform card, int index, int count)
		{
			card.anchorMin = new Vector2(0.5f, 0.5f);
			card.anchorMax = new Vector2(0.5f, 0.5f);
			card.pivot = new Vector2(0.5f, 0.5f);
			card.sizeDelta = _communityCardSize;

			var offset = (index - (count - 1) * 0.5f) * _communityCardSpacing;

			if (_communityFanRadius <= 0f)
			{
				card.anchoredPosition = new Vector2(offset, 0f);
				card.localRotation = Quaternion.identity;
				return;
			}

			// Clamped because a board wider than the fan is deep has no angle to sit at, and asin would
			// hand back NaN rather than saying so.
			var angle = Mathf.Asin(Mathf.Clamp(offset / _communityFanRadius, -1f, 1f));

			card.anchoredPosition = new Vector2(offset, _communityFanRadius * (Mathf.Cos(angle) - 1f));
			card.localRotation = Quaternion.Euler(0f, 0f, -angle * Mathf.Rad2Deg);
		}

		private void HandleClose()
		{
			_dismissed = true;
			Refresh();
		}

		private void Refresh()
		{
			var showdown = Data.Showdown;

			// The showdown emptying is the hand being put away, and that is what makes the next ranking a
			// new one to be shown rather than the one this player already waved off.
			if (showdown.Count == 0) _dismissed = false;

			var visible = showdown.Count > 0 && !_dismissed;

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

			while (_communityCards.Count < cards.Count) _communityCards.Add(Instantiate(_cardPrefab, _communityContainer));

			for (var i = 0; i < _communityCards.Count; i++)
			{
				var used = i < cards.Count;
				_communityCards[i].gameObject.SetActive(used);

				if (!used) continue;

				// Re-placed on every pass rather than once at creation: where a card sits in the fan is a
				// function of how many there are, and a board growing from three to five moves all of them.
				PlaceOnFan((RectTransform)_communityCards[i].transform, i, cards.Count);

				// The board is on the table whole from the deal, so a hand that ended before the river shows
				// the cards it never turned as backs rather than spoiling them here.
				_communityCards[i].SetCard(cards[i], Data.IsCommunityCardVisible(i));
			}
		}
	}
}
