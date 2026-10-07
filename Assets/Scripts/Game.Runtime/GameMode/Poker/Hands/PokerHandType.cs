using System.Collections.Generic;
using Localization;
using UnityEngine;
using UnityEngine.Serialization;

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
		[LocalizationKey]
		[FormerlySerializedAs("_displayName")]
		[SerializeField] private string _nameKey;

		[Tooltip("Higher beats lower. Standard hands run 0 (high card) to 9 (royal flush).")]
		[SerializeField] private int _tier;

		[Tooltip("A hand only a house rule makes (Five of a Kind, off Jokers). The helper crowns it above the standard ranking.")]
		[SerializeField] private bool _isHouseHand;

		[Header("Helper")]
		[LocalizationKey]
		[FormerlySerializedAs("_description")]
		[SerializeField] private string _descriptionKey;

		[Tooltip("The cards that make the hand, in the order they are shown.")]
		[SerializeField] private List<PokerHandExampleCard> _exampleCards = new();

		public string NameKey => _nameKey;
		public string DisplayName => string.IsNullOrEmpty(_nameKey) ? name : Localizer.Get(_nameKey);
		public int Tier => _tier;
		public bool IsHouseHand => _isHouseHand;

		public string GetName() => DisplayName;

		public string GetDescription() => Localizer.Get(_descriptionKey);

		public void GetExampleCards(List<CardData> cards)
		{
			cards.Clear();

			foreach (var card in _exampleCards) cards.Add(card.ToCardData());
		}

		// Kickers are appended highest first and decide ties between two hands of the same tier.
		public abstract bool TryEvaluate(PokerCardAnalysis analysis, List<int> kickers);
	}
}
