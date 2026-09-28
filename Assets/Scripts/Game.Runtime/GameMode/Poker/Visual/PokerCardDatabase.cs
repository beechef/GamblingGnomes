using System.Collections.Generic;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Visual
{
	[CreateAssetMenu(fileName = "PokerCardDatabase", menuName = "Game/Poker Card Database")]
	public class PokerCardDatabase : ScriptableObject
	{
		[SerializeField] private Sprite _cardBack;

		[Tooltip("52 faces in CardData.DatabaseIndex order: clubs, diamonds, hearts, spades, each running two through ace.")]
		[SerializeField] private List<Sprite> _cardFaces = new();

		[Tooltip("The one face every Joker wears.")]
		[SerializeField] private Sprite _joker;

		public Sprite CardBack => _cardBack;

		public Sprite GetFace(CardData card)
		{
			if (!card.IsValid) return _cardBack;
			if (card.IsJoker) return _joker ? _joker : _cardBack;

			var index = card.DatabaseIndex;
			return index >= 0 && index < _cardFaces.Count ? _cardFaces[index] : _cardBack;
		}

		public Sprite GetSprite(CardData card, bool faceUp) => faceUp ? GetFace(card) : _cardBack;
	}
}
