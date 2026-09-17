using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Hands
{
	// A card as somebody authoring it thinks of it — a rank and a named suit — rather than the two bytes
	// CardData replicates as.
	[Serializable]
	public struct PokerHandExampleCard
	{
		[PropertyRange(CardData.LowestRank, CardData.HighestRank)]
		[Tooltip("2 to 10, then 11 jack, 12 queen, 13 king, 14 ace.")]
		[SerializeField] private int _rank;

		[SerializeField] private CardSuit _suit;

		public PokerHandExampleCard(int rank, CardSuit suit)
		{
			_rank = rank;
			_suit = suit;
		}

		public CardData ToCardData() => new((byte)Mathf.Clamp(_rank, CardData.LowestRank, CardData.HighestRank), _suit);
	}
}
