using Game.Runtime.GameMode.Poker;
using TMPro;
using UnityEngine;

namespace Game.Runtime.UI.Poker
{
	// One exposed card in a known-cards row: the card, and a word under it about how it came out.
	public class UIPokerKnownCardEntry : MonoBehaviour
	{
		[SerializeField] private UIPokerCard _card;

		[Tooltip("Optional. Who saw it, or that the whole table did.")]
		[SerializeField] private TMP_Text _label;

		public void Bind(CardData card, string label)
		{
			if (_card) _card.SetCard(card);

			if (!_label) return;

			_label.text = label;
			_label.gameObject.SetActive(!string.IsNullOrEmpty(label));
		}
	}
}
