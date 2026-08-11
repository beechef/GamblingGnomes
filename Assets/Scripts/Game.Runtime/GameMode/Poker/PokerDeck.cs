using System.Collections.Generic;

namespace Game.Runtime.GameMode.Poker
{
	// Server side only — the deck never leaves the host, clients only ever see the cards dealt to them.
	public class PokerDeck
	{
		private readonly List<CardData> _cards = new();
		private int _nextIndex;

		public int Remaining => _cards.Count - _nextIndex;

		public void Rebuild()
		{
			_cards.Clear();
			_nextIndex = 0;

			for (var suit = 0; suit < 4; suit++)
			{
				for (var rank = CardData.LowestRank; rank <= CardData.HighestRank; rank++)
				{
					_cards.Add(new CardData(rank, (CardSuit)suit));
				}
			}
		}

		public void Shuffle()
		{
			for (var i = _cards.Count - 1; i > 0; i--)
			{
				var j = UnityEngine.Random.Range(0, i + 1);
				(_cards[i], _cards[j]) = (_cards[j], _cards[i]);
			}
		}

		public CardData Draw()
		{
			if (Remaining <= 0) return CardData.None;

			var card = _cards[_nextIndex];
			_nextIndex++;
			return card;
		}
	}
}
