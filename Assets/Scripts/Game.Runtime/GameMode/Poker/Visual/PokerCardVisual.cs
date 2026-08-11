using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Visual
{
	public class PokerCardVisual : MonoBehaviour
	{
		[SerializeField] private SpriteRenderer _renderer;
		[SerializeField] private PokerCardDatabase _database;

		public CardData Card { get; private set; }
		public bool FaceUp { get; private set; }

		public void SetCard(CardData card, bool faceUp, PokerCardDatabase database = null)
		{
			if (database) _database = database;

			Card = card;
			FaceUp = faceUp;

			if (_renderer && _database) _renderer.sprite = _database.GetSprite(card, faceUp);
		}
	}
}
