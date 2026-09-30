using System;
using System.Collections.Generic;
using Game.Runtime.Props;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Visual
{
	// What a card's looks mean: each one is another deck the pictures are cut from. The card answers for itself,
	// so a hallucination only names the look and never hands the card a set of sprites. Every deck lives in the
	// one card atlas, because the card shaders read a single global texture.
	public class PokerCardDeckLook : MonoBehaviour
	{
		[Serializable]
		private struct Deck
		{
			public PropVariant Variant;
			public PokerCardDatabase Database;
		}

		[Required]
		[SerializeField] private PropVariantController _variants;

		[Required]
		[SerializeField] private PokerCardVisual _card;

		[Tooltip("The deck each look draws from. A look not listed here keeps the deck the card was dealt with.")]
		[SerializeField] private List<Deck> _decks = new();

		private void OnEnable()
		{
			if (!_variants) return;

			_variants.OnVariantChanged += HandleVariantChanged;

			// A card dealt while the look is already on arrives in it.
			HandleVariantChanged(_variants.Current);
		}

		private void OnDisable()
		{
			if (_variants) _variants.OnVariantChanged -= HandleVariantChanged;
		}

		private void HandleVariantChanged(PropVariant current)
		{
			if (!_card) return;

			PokerCardDatabase deck = null;

			foreach (var entry in _decks)
			{
				if (entry.Variant != current) continue;

				deck = entry.Database;
				break;
			}

			_card.SetDeck(deck);
		}
	}
}
