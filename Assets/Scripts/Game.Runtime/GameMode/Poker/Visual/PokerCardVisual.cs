using DG.Tweening;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Visual
{
	// Two quads back to back, because a card is a physical object: a face on one side, the back pattern
	// on the other, half the card's thickness either side of the middle so the pair is not coplanar and
	// cannot z-fight.
	//
	// How big they are, where they sit and what the hit box measures are all authored in the prefab —
	// every card in the deck is the same size, so none of it is a runtime question, and a runtime that
	// worked it out again would be a second answer to a settled question and the one nobody can look at.
	// This says which picture goes on which face and nothing else.
	public class PokerCardVisual : MonoBehaviour
	{
		[Header("Renderers")]
		[Tooltip("The face. A quad rather than a sprite, because a SpriteRenderer draws one material and nothing else: an effect hanging a second pass on a card would be stored and never rendered.")]
		[SerializeField] private MeshRenderer _frontRenderer;

		[SerializeField] private MeshRenderer _backRenderer;
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

		[Header("Placement")]
		[Tooltip("Seconds a card takes to travel between the table and a hand.")]
		[SerializeField] private float _moveDuration = 0.4f;

		[SerializeField] private Ease _moveEase = Ease.OutCubic;

		[Tooltip("How high the card arcs on its way. A card sliding flat across the table reads as a bug rather than as a pickup.")]
		[SerializeField] private float _moveArc = 0.08f;

		private Tween _flipTween;
		private Tween _moveTween;
		private Tween _liftTween;
		private bool _initialized;

		// How far off the table the art is standing, and why. Two reasons, one height: a card being
		// hovered or chosen, and a card mid-flip arcing over the surface it is lying on. They are summed
		// and written in one place, because a card can only be at one height and two tweens writing the
		// same localPosition would each erase the other's answer.
		private float _lift;
		private float _flipLift;

		// One block, reused. Each card shows a different picture, so the texture is per-renderer state rather
		// than per-material — a material each would be one more material per card in the deal.
		private static MaterialPropertyBlock _block;

		private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");

		public CardData Card { get; private set; }
		public bool FaceUp { get; private set; } = true;

		// Whether this card is one the player may reach for right now. Set by the beat that allows picking
		// and cleared when it ends, so a card is not offered outside the moment the rules open for it —
		// the server would refuse the pick anyway, and a card that lifts under the cursor and then does
		// nothing is worse than one that never lifted.
		public bool Pickupable { get; set; }

		// How big this card is, in the units of whatever is laying it out. A group that arranges cards has
		// to know — a fan turns about a point half a card below the middle — and the card is the only thing
		// that does: the quad, its scale and the hit box are all authored in the prefab. An arrangement
		// carrying its own copy of the number is a second place to change when the deck is resized, and the
		// one that silently keeps the old shape.
		//
		// Measured off the face quad, whose mesh is Unity's own 1 x 1, so its lossy scale is its size. The
		// division converts that back out of this card's own scale, which is what the parent applies anyway.
		public Vector2 Size
		{
			get
			{
				var face = _frontRenderer ? _frontRenderer.transform : FlipRoot;
				var world = face.lossyScale;
				var lossy = transform.lossyScale;
				var local = transform.localScale;

				return new Vector2(InParentUnits(world.x, lossy.x, local.x), InParentUnits(world.y, lossy.y, local.y));
			}
		}

		private static float InParentUnits(float world, float lossy, float local)
			=> Mathf.Approximately(lossy, 0f) ? world : world * local / lossy;

		private Transform FlipRoot => _flipRoot ? _flipRoot : transform;

		private void OnDestroy()
		{
			_flipTween?.Kill();
			_moveTween?.Kill();
			_liftTween?.Kill();
		}

		// Where the card sits, and under what. Reparenting keeps the world pose so the travel starts from
		// wherever the card actually was — a card picked up off the table must not jump to the hand and
		// then animate from there. The arc is along the table's own up rather than the card's facing,
		// because a card being lifted rises off the surface it was lying on.
		public void PlaceAt(Transform parent, Vector3 localPosition, Quaternion localRotation, bool animate)
		{
			_moveTween?.Kill();

			// A card that is travelling is not a card being hovered, so the lift is dropped before the rest
			// changes.
			_liftTween?.Kill();
			_lift = 0f;
			ApplyArtHeight();

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
				Draw(_frontRenderer, faceUp ? _database.GetFace(card) : _database.CardBack);
				Draw(_backRenderer, _database.CardBack);
			}

			Flip(faceUp, animateFlip);
		}

		// A card's picture onto its quad, and nothing else. How big the quad is, where the two faces sit and
		// what the hit box measures are the same on every card in the deck, so they are authored in the
		// prefab where they can be seen and tuned — a runtime working them out again is a second answer to
		// a settled question, and the one nobody can look at.
		private static void Draw(MeshRenderer renderer, Sprite sprite)
		{
			if (!renderer) return;

			// Nothing to show is switched off rather than left holding the last card's face.
			if (!sprite || !sprite.texture)
			{
				renderer.enabled = false;
				return;
			}

			renderer.enabled = true;

			_block ??= new MaterialPropertyBlock();

			renderer.GetPropertyBlock(_block);
			_block.SetTexture(BaseMapId, sprite.texture);
			renderer.SetPropertyBlock(_block);

		}

		// How far the card stands off the table: under the cursor, or chosen and waiting for the rest of the
		// hand to be chosen with it. One number rather than one per reason, because a card can only be at one
		// height and whoever is asking already knows which of the two it is.
		//
		// It moves the art and never the root, because the root is what carries the collider. Lifting the
		// hit box along with the picture is a feedback loop: a card hovered near its own edge rises out
		// from under the cursor, stops being hovered, drops back under it, and is hovered again — which
		// reads as a card flickering rather than as a hit region that moved.
		public void SetLift(float lift)
		{
			if (Mathf.Approximately(_lift, lift)) return;

			_liftTween?.Kill();

			var from = _lift;
			_liftTween = DOVirtual.Float(from, lift, 0.12f, value =>
				{
					_lift = value;
					ApplyArtHeight();
				})
				.SetEase(Ease.OutCubic);
		}

		// The one writer of how high the art sits. Both reasons land here rather than each tweening the
		// same localPosition, which is how one of them silently wins.
		private void ApplyArtHeight()
		{
			var root = FlipRoot;
			if (root) root.localPosition = new Vector3(0f, 0f, -(_lift + _flipLift));
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
				_flipLift = 0f;
				ApplyArtHeight();
				return;
			}

			var from = faceUp ? FaceDownRotation : FaceUpRotation;
			root.localRotation = from;
			_flipLift = 0f;
			ApplyArtHeight();

			// Driven by an explicit slerp rather than a quaternion tween: turning exactly 180 degrees
			// leaves the two rotations orthogonal in quaternion space, where "shortest path" is
			// ambiguous and the card can settle back the way it came. The lift rides the same t, along
			// the card's own facing, so a board card rises off the table instead of dipping through it.
			_flipTween = DOVirtual.Float(0f, 1f, _flipDuration, t =>
				{
					if (!root) return;

					root.localRotation = Quaternion.Slerp(from, target, t);
					_flipLift = Mathf.Sin(t * Mathf.PI) * _flipHeight;
					ApplyArtHeight();
				})
				.SetEase(_flipEase)
				.OnComplete(() =>
				{
					if (!root) return;

					root.localRotation = target;
					_flipLift = 0f;
					ApplyArtHeight();
				});
		}
	}
}
