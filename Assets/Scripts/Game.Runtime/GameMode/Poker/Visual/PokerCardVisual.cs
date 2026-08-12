using DG.Tweening;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Visual
{
	// Two renderers back to back, because a card is a physical object: a face on one side, the back
	// pattern on the other. A single sprite is invisible from behind, which reads as a card that
	// vanishes whenever the table is seen from the other side.
	public class PokerCardVisual : MonoBehaviour
	{
		[Header("Renderers")]
		[SerializeField] private SpriteRenderer _frontRenderer;
		[SerializeField] private SpriteRenderer _backRenderer;
		[SerializeField] private PokerCardDatabase _database;

		[Header("Flip")]
		[Tooltip("What actually turns over. Kept separate from the root so the fan and layout rotations are not fighting the flip.")]
		[SerializeField] private Transform _flipRoot;

		[SerializeField] private float _flipDuration = 0.35f;
		[SerializeField] private Ease _flipEase = Ease.OutCubic;

		[Tooltip("Lift while turning, so a card coming off the table arcs instead of grinding through it.")]
		[SerializeField] private float _flipHeight = 0.02f;

		// A sprite is readable from its -Z side — that is where Unity's 2D camera sits — so the card's
		// face looks down its own -Z, and everything that moves a card sideways or lifts it has to
		// travel that way too or it goes through whatever the card is lying on.
		private static readonly Quaternion FaceUpRotation = Quaternion.identity;
		private static readonly Quaternion FaceDownRotation = Quaternion.Euler(0f, 180f, 0f);

		private Tween _flipTween;
		private bool _initialized;

		public CardData Card { get; private set; }
		public bool FaceUp { get; private set; } = true;

		private Transform FlipRoot => _flipRoot ? _flipRoot : transform;

		private void OnDestroy() => _flipTween?.Kill();

		public void SetCard(CardData card, bool faceUp, PokerCardDatabase database = null, bool animateFlip = false)
		{
			if (database) _database = database;

			// A NetworkList raises one event per element, so a three card flop refreshes the board three
			// times. Without this, the cards already turned over get re-set mid flip and their tween is
			// killed underneath them.
			if (!animateFlip && _initialized && faceUp == FaceUp && card.Equals(Card)) return;

			_initialized = true;
			Card = card;

			if (_database)
			{
				// A hand this client may not see shows its back from both sides rather than a blank face.
				if (_frontRenderer) _frontRenderer.sprite = faceUp ? _database.GetFace(card) : _database.CardBack;
				if (_backRenderer) _backRenderer.sprite = _database.CardBack;
			}

			Flip(faceUp, animateFlip);
		}

		// Turns the card over. Animated, it always starts from the opposite side, so revealing a card
		// plays as the dealer turning it rather than the art simply changing.
		public void Flip(bool faceUp, bool animate)
		{
			_flipTween?.Kill();

			FaceUp = faceUp;

			var root = FlipRoot;
			var target = faceUp ? FaceUpRotation : FaceDownRotation;

			if (!animate)
			{
				root.localRotation = target;
				root.localPosition = Vector3.zero;
				return;
			}

			var from = faceUp ? FaceDownRotation : FaceUpRotation;
			root.localRotation = from;
			root.localPosition = Vector3.zero;

			// Driven by an explicit slerp rather than a quaternion tween: turning exactly 180 degrees
			// leaves the two rotations orthogonal in quaternion space, where "shortest path" is
			// ambiguous and the card can settle back the way it came. The lift rides the same t, along
			// the card's own facing, so a board card rises off the table instead of dipping through it.
			_flipTween = DOVirtual.Float(0f, 1f, _flipDuration, t =>
				{
					if (!root) return;

					root.localRotation = Quaternion.Slerp(from, target, t);
					root.localPosition = new Vector3(0f, 0f, -Mathf.Sin(t * Mathf.PI) * _flipHeight);
				})
				.SetEase(_flipEase)
				.OnComplete(() =>
				{
					if (!root) return;

					root.localRotation = target;
					root.localPosition = Vector3.zero;
				});
		}
	}
}
