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

		[Header("Picking")]
		[Tooltip("What a raycast hits. Sized from the sprite whenever the card is set, because a card only knows how big it is once it has a face.")]
		[SerializeField] private BoxCollider _collider;

		[Tooltip("Thickness of that box. A card is flat, but a zero-depth box is a raycast target that can be missed edge on.")]
		[SerializeField] private float _colliderDepth = 0.004f;

		[Header("Placement")]
		[Tooltip("Seconds a card takes to travel between the table and a hand.")]
		[SerializeField] private float _moveDuration = 0.4f;

		[SerializeField] private Ease _moveEase = Ease.OutCubic;

		[Tooltip("How high the card arcs on its way. A card sliding flat across the table reads as a bug rather than as a pickup.")]
		[SerializeField] private float _moveArc = 0.08f;

		private Tween _flipTween;
		private Tween _moveTween;
		private Tween _hoverTween;
		private bool _initialized;
		private float _lift;
		private float _restLocalZ;

		public CardData Card { get; private set; }
		public bool FaceUp { get; private set; } = true;

		// Whether this card is one the player may reach for right now. Set by the beat that allows picking
		// and cleared when it ends, so a card is not offered outside the moment the rules open for it —
		// the server would refuse the pick anyway, and a card that lifts under the cursor and then does
		// nothing is worse than one that never lifted.
		public bool Pickupable { get; set; }

		private Transform FlipRoot => _flipRoot ? _flipRoot : transform;

		private void OnDestroy()
		{
			_flipTween?.Kill();
			_moveTween?.Kill();
			_hoverTween?.Kill();
		}

		// Where the card sits, and under what. Reparenting keeps the world pose so the travel starts from
		// wherever the card actually was — a card picked up off the table must not jump to the hand and
		// then animate from there. The arc is along the table's own up rather than the card's facing,
		// because a card being lifted rises off the surface it was lying on.
		public void PlaceAt(Transform parent, Vector3 localPosition, Quaternion localRotation, bool animate)
		{
			_moveTween?.Kill();

			// A card that is travelling is not a card being hovered: the lift is measured off wherever the
			// card comes to rest, so it has to be forgotten before the rest changes.
			_hoverTween?.Kill();
			_lift = 0f;
			_restLocalZ = localPosition.z;

			if (transform.parent != parent) transform.SetParent(parent, true);

			if (!animate)
			{
				transform.localPosition = localPosition;
				transform.localRotation = localRotation;
				return;
			}

			var fromPosition = transform.localPosition;
			var fromRotation = transform.localRotation;
			var lift = parent ? parent.InverseTransformVector(Vector3.up) * _moveArc : Vector3.up * _moveArc;

			_moveTween = DOVirtual.Float(0f, 1f, _moveDuration, t =>
				{
					transform.localPosition = Vector3.Lerp(fromPosition, localPosition, t) + lift * Mathf.Sin(t * Mathf.PI);
					transform.localRotation = Quaternion.Slerp(fromRotation, localRotation, t);
				})
				.SetEase(_moveEase)
				.OnComplete(() =>
				{
					transform.localPosition = localPosition;
					transform.localRotation = localRotation;
				});
		}

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
			ResizeCollider();
		}

		// How far the card stands off the table: under the cursor, or chosen and waiting for the rest of the
		// hand to be chosen with it. One number rather than one per reason, because a card can only be at one
		// height and whoever is asking already knows which of the two it is.
		// Applied to the root rather than the flip root, which the flip owns outright.
		public void SetLift(float lift)
		{
			if (Mathf.Approximately(_lift, lift)) return;

			_lift = lift;
			_hoverTween?.Kill();
			_hoverTween = transform.DOLocalMoveZ(_restLocalZ - lift, 0.12f).SetEase(Ease.OutCubic);
		}

		// The sprite is assigned at runtime from the database, so the prefab has no size to author against.
		// Taken off the front rather than the back: they are the same card, and the front is the one that
		// is always set.
		private void ResizeCollider()
		{
			if (!_collider || !_frontRenderer || !_frontRenderer.sprite) return;

			var size = _frontRenderer.sprite.bounds.size;
			_collider.size = new Vector3(size.x, size.y, Mathf.Max(0.0001f, _colliderDepth));
			_collider.center = _frontRenderer.sprite.bounds.center;
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
