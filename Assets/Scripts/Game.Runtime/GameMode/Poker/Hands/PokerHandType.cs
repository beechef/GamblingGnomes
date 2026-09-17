using System.Collections.Generic;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Hands
{
	// One asset per hand. Tier is the whole ranking: a house rule that beats a straight flush is a new
	// asset with a higher tier, and nothing else in the game has to know it exists.
	//
	// The hand also says how it is explained — its name, a line of rules and a sample of the cards that make
	// it — so a new hand turns up in the helper the moment it is in the database, with nobody keeping a
	// second list of what the table recognises.
	public abstract class PokerHandType : ScriptableObject
	{
		[Header("Hand")]
		[SerializeField] private string _displayName;

		[Tooltip("Higher beats lower. Standard hands run 0 (high card) to 8 (straight flush).")]
		[SerializeField] private int _tier;

		[Header("Helper")]
		[TextArea]
		[SerializeField] private string _description;

		[Tooltip("The cards that make the hand, in the order they are shown.")]
		[SerializeField] private List<PokerHandExampleCard> _exampleCards = new();

		public string DisplayName => string.IsNullOrEmpty(_displayName) ? name : _displayName;
		public int Tier => _tier;

		public string GetName() => DisplayName;

		public string GetDescription() => _description;

		public void GetExampleCards(List<CardData> cards)
		{
			cards.Clear();

			foreach (var card in _exampleCards) cards.Add(card.ToCardData());
		}

		// Kickers are appended highest first and decide ties between two hands of the same tier.
		public abstract bool TryEvaluate(PokerCardAnalysis analysis, List<int> kickers);
	}
}
