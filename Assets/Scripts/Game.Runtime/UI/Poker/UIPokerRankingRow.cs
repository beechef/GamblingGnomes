using System.Collections.Generic;
using Game.Runtime.GameMode.Poker;
using Game.Runtime.GameMode.Poker.Player;
using TMPro;
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

		public void SetEntry(PokerShowdownEntry entry, PokerPlayer player)
		{
			if (_placeLabel) _placeLabel.text = Ordinal(entry.Rank);
			if (_handLabel) _handLabel.text = entry.HandName.ToString();

			if (_nameLabel)
			{
				var wallet = player ? player.Wallet : null;
				_nameLabel.text = wallet ? wallet.DisplayName.Value.ToString() : $"Player {entry.ClientId}";
			}

			// Nothing won is left blank rather than shown as a zero — a losing row should read as quiet.
			if (_winningsLabel) _winningsLabel.text = entry.Winnings > 0 ? "+" + entry.Winnings : string.Empty;

			RebuildCards(player);
		}

		private void RebuildCards(PokerPlayer player)
		{
			if (!_cardContainer || !_cardPrefab) return;

			var data = player ? player.Data : null;
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

				if (visible) _cards[i].SetCard(data.HoleCards[i]);
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
