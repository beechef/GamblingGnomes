using System;
using System.Collections.Generic;

namespace Game.Runtime.GameMode.Poker
{
	// Server side only — the deck never leaves the host, clients only ever see the cards dealt to them. How
	// many are left is public, so the owner of the table replicates that count off OnRemainingChanged.
	// A queue, because a deck is only ever dealt from the top: drawing takes the card off.
	public class PokerDeck
	{
		private readonly Queue<CardData> _cards = new();
		private readonly List<CardData> _shuffleBuffer = new();

		public int Remaining => _cards.Count;

		public event Action OnRemainingChanged;

		public void Rebuild()
		{
			_cards.Clear();

			for (var suit = 0; suit < 4; suit++)
			{
				for (var rank = CardData.LowestRank; rank <= CardData.HighestRank; rank++)
				{
					_cards.Enqueue(new CardData(rank, (CardSuit)suit));
				}
			}

			OnRemainingChanged?.Invoke();
		}

		// A queue has no order to swap in place, so the cards are laid out, shuffled and stacked back.
		public void Shuffle()
		{
			_shuffleBuffer.Clear();
			_shuffleBuffer.AddRange(_cards);

			for (var i = _shuffleBuffer.Count - 1; i > 0; i--)
			{
				var j = UnityEngine.Random.Range(0, i + 1);
				(_shuffleBuffer[i], _shuffleBuffer[j]) = (_shuffleBuffer[j], _shuffleBuffer[i]);
			}

			_cards.Clear();
			foreach (var card in _shuffleBuffer) _cards.Enqueue(card);

			_shuffleBuffer.Clear();
		}

		public int CountRemaining(CardSuit suit)
		{
			var count = 0;
			foreach (var card in _cards)
			{
				if (card.SuitType == suit) count++;
			}

			return count;
		}

		public CardData Draw()
		{
			if (!_cards.TryDequeue(out var card)) return CardData.None;

			OnRemainingChanged?.Invoke();
			return card;
		}
	}
}
