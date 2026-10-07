using Game.Runtime.GameMode.Poker;
using UnityEngine;

namespace Game.Runtime.UI.Poker
{
	// One exposed card in a known-cards row.
	public class UIPokerKnownCardEntry : MonoBehaviour
	{
		[SerializeField] private UIPokerCard _card;

		public void Bind(CardData card)
		{
			if (_card) _card.SetCard(card);
		}
	}
}
