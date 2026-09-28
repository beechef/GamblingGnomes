using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Hands
{
	// A card as somebody authoring it thinks of it — a rank and a named suit, or a Joker — rather than the
	// two bytes CardData replicates as.
	[Serializable]
	public struct PokerHandExampleCard
	{
		[SerializeField] private bool _isJoker;

		[HideIf(nameof(_isJoker))]
		[PropertyRange(CardData.LowestRank, CardData.HighestRank)]
		[Tooltip("2 to 10, then 11 jack, 12 queen, 13 king, 14 ace.")]
		[SerializeField] private int _rank;

		[HideIf(nameof(_isJoker))]
		[SerializeField] private CardSuit _suit;

		public PokerHandExampleCard(int rank, CardSuit suit)
		{
			_isJoker = false;
			_rank = rank;
			_suit = suit;
		}

		public CardData ToCardData() =>
			_isJoker ? CardData.Joker : new CardData((byte)Mathf.Clamp(_rank, CardData.LowestRank, CardData.HighestRank), _suit);
	}
}
