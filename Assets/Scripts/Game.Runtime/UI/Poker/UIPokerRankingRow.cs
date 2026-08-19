using System.Collections.Generic;
using Game.Runtime.GameMode.Poker;
using Game.Runtime.GameMode.Poker.Player;
using TMPro;
using Unity.Netcode;
using UnityEngine;

namespace Game.Runtime.UI.Poker
{
	// One finishing place on the showdown board. It draws whatever it is handed and nothing else — the
	// panel decides who belongs on which row.
	public class UIPokerRankingRow : MonoBehaviour
	{
		[Header("Labels")]
		[SerializeField] private TextMeshProUGUI _placeLabel;
		[SerializeField] private TextMeshProUGUI _nameLabel;
		[SerializeField] private TextMeshProUGUI _handLabel;
		[SerializeField] private TextMeshProUGUI _winningsLabel;

		[Header("Cards")]
		[Tooltip("Where this row's hole cards are laid out.")]
		[SerializeField] private RectTransform _cardContainer;

		[SerializeField] private UIPokerCard _cardPrefab;

		[Tooltip("Cards are sized here rather than by the layout group. A card has a fixed shape, and a group that stretches one axis without the other flattens it.")]
		[SerializeField] private Vector2 _cardSize = new(44f, 66f);

		private readonly List<UIPokerCard> _cards = new();

		private PokerPlayer _player;

		// The row is handed its place by the panel, and then watches the player it drew. The showdown list
		// and the reveal it implies live on two different network objects, so they land in either order —
		// the board arriving first and never looking again is what left the winner's hand face down on
		// everyone else's screen.
		public void SetEntry(PokerShowdownEntry entry, PokerPlayer player)
		{
			Bind(player);

			if (_placeLabel) _placeLabel.text = Ordinal(entry.Rank);
			if (_handLabel) _handLabel.text = entry.HandName.ToString();

			if (_nameLabel) _nameLabel.text = player ? player.DisplayName : $"Player {entry.ClientId}";

			// Nothing won is left blank rather than shown as a zero — a losing row should read as quiet.
			if (_winningsLabel) _winningsLabel.text = entry.Winnings > 0 ? "+" + entry.Winnings : string.Empty;

			RebuildCards();
		}

		private void OnDisable() => Bind(null);

		private void Bind(PokerPlayer player)
		{
			if (_player == player) return;

			if (_player && _player.Data)
			{
				_player.Data.OnStateChanged -= RebuildCards;
				_player.Data.OnHoleCardsChanged -= HandleHoleCardsChanged;
			}

			_player = player;

			if (!_player || !_player.Data) return;

			_player.Data.OnStateChanged += RebuildCards;
			_player.Data.OnHoleCardsChanged += HandleHoleCardsChanged;
		}

		private void HandleHoleCardsChanged(NetworkListEvent<CardData> change) => RebuildCards();

		private void RebuildCards()
		{
			if (!_cardContainer || !_cardPrefab) return;

			var data = _player ? _player.Data : null;
			var count = data ? data.CardCount : 0;

			while (_cards.Count < count)
			{
				var card = Instantiate(_cardPrefab, _cardContainer);
				((RectTransform)card.transform).sizeDelta = _cardSize;

				_cards.Add(card);
			}

			for (var i = 0; i < _cards.Count; i++)
			{
				var visible = i < count;
				_cards[i].gameObject.SetActive(visible);

				// A hand that never had to show — a win by folds — keeps its back on the board too;
				// drawing it face up here would undo the reveal rule the server just applied.
				if (visible) _cards[i].SetCard(data.HoleCards[i], data.IsHandVisible);
			}
		}

		private static string Ordinal(int rank)
		{
			var suffix = rank switch
			{
				1 => "st",
				2 => "nd",
				3 => "rd",
				_ => "th"
			};

			// The teens all take "th" however they end.
			if (rank % 100 is >= 11 and <= 13) suffix = "th";

			return rank + suffix;
		}
	}
}
