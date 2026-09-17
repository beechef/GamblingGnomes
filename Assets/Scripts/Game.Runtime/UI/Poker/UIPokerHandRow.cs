using System.Collections.Generic;
using Game.Runtime.GameMode.Poker;
using Game.Runtime.GameMode.Poker.Hands;
using TMPro;
using UnityEngine;

namespace Game.Runtime.UI.Poker
{
	// One hand in the helper: where it ranks, what it is called, what makes it, and a sample of the cards.
	// Everything it draws is asked of the hand itself, so the row carries no poker knowledge of its own.
	//
	// The sample cards overlap in a layout row with negative spacing. The row lays them out right to left
	// and the first card is the last sibling, so the leftmost card is drawn on top of the ones behind it.
	public class UIPokerHandRow : MonoBehaviour
	{
		[Header("References")]
		[SerializeField] private TMP_Text _rankLabel;
		[SerializeField] private TMP_Text _nameLabel;
		[SerializeField] private TMP_Text _descriptionLabel;

		[Tooltip("Cards authored under the card row; as many are shown as the hand's sample holds.")]
		[SerializeField] private List<UIPokerCard> _cards = new();

		private readonly List<CardData> _exampleCards = new();

		public void Bind(int rank, PokerHandType hand)
		{
			if (_rankLabel) _rankLabel.text = rank.ToString();
			if (_nameLabel) _nameLabel.text = hand.GetName();
			if (_descriptionLabel) _descriptionLabel.text = hand.GetDescription();

			hand.GetExampleCards(_exampleCards);

			// _cards[0] is the last sibling, so it lands leftmost and on top.
			for (var i = 0; i < _cards.Count; i++)
			{
				var card = _cards[i];
				if (!card) continue;

				var shown = i < _exampleCards.Count;
				card.gameObject.SetActive(shown);
				if (shown) card.SetCard(_exampleCards[i]);
			}
		}
	}
}
