using Game.Runtime.GameMode.Poker;
using Game.Runtime.GameMode.Poker.Visual;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Runtime.UI.Poker
{
	// One card on a canvas. It owns the lookup from card to sprite and carries its own database, so the
	// views that lay cards out only have to say which card goes where — before this, every one of them
	// kept its own database reference and its own copy of the same two lines.
	public class UIPokerCard : MonoBehaviour
	{
		[Header("References")]
		[SerializeField] private Image _image;

		[Tooltip("Where the faces come from. Set on the prefab so a view never has to hand one over.")]
		[SerializeField] private PokerCardDatabase _database;

		public CardData Card { get; private set; }
		public bool IsFaceUp { get; private set; } = true;

		private void Awake()
		{
			if (!_image) _image = GetComponent<Image>();
		}

		public void SetCard(CardData card, bool faceUp = true)
		{
			Card = card;
			IsFaceUp = faceUp;

			Apply();
		}

		public void SetFaceUp(bool faceUp)
		{
			if (IsFaceUp == faceUp) return;

			IsFaceUp = faceUp;
			Apply();
		}

		private void Apply()
		{
			if (!_image || !_database) return;

			_image.sprite = _database.GetSprite(Card, IsFaceUp);
		}
	}
}
