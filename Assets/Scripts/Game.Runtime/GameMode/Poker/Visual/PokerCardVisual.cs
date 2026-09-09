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

		[Header("Picking")]
		[Tooltip("What a raycast hits. Sized from the sprite whenever the card is set, because a card only knows how big it is once it has a face. It belongs on the root, which only ever moves when the card really travels — the lift and the flip move the art below it, so the hit region cannot slide out from under the cursor that is pointing at it.")]
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
		private Tween _liftTween;
		private bool _initialized;

		// How far off the table the art is standing, and why. Two reasons, one height: a card being
		// hovered or chosen, and a card mid-flip arcing over the surface it is lying on. They are summed
		// and written in one place, because a card can only be at one height and two tweens writing the
		// same localPosition would each erase the other's answer.
		private float _lift;
		private float _flipLift;

		// The face this card is currently showing. Kept because the sprite is what knows how big a card is,
		// and the renderer no longer holds one.
		private Sprite _face;

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
				_face = faceUp ? _database.GetFace(card) : _database.CardBack;

				Draw(_frontRenderer, _face);
				Draw(_backRenderer, _database.CardBack);
			}

			Flip(faceUp, animateFlip);
			ResizeCollider();
		}

		// A card's picture onto its quad. One texture per card, so the whole of it is the card and the UVs
		// are the quad's own — no atlas rectangle to carry, which is a tiling and offset that has to be
		// right on every card and is wrong in silence when it is not. The quad is 1x1, so scaling it by the
		// sprite's own bounds reproduces exactly what the SpriteRenderer drew.
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

			var size = sprite.bounds.size;
			renderer.transform.localScale = new Vector3(size.x, size.y, 1f);
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

		// The sprite is assigned at runtime from the database, so the prefab has no size to author against.
		// Taken off the front rather than the back: they are the same card, and the front is the one that
		// is always set.
		//
		// A sprite's bounds are in the renderer's own space, and the collider sits on the root above it —
		// the art below is scaled down to a card's real size, so the same numbers mean different things on
		// the two objects. Read straight across, the box came out at the scale the *sprite* is drawn at,
		// which is more than a metre wide and swallows the whole table. The ratio between the two lossy
		// scales converts it, and cancels any scale an anchor above adds along with it.
		private void ResizeCollider()
		{
			if (!_collider || !_frontRenderer || !_face) return;

			// Measured against the pivot, not the renderer: the quad now carries the card's size in its own
			// localScale, so asking the renderer would count that size twice.
			var factor = RelativeScale(FlipRoot, _collider.transform);
			var bounds = _face.bounds;

			var size = Vector3.Scale(bounds.size, factor);

			_collider.size = new Vector3(size.x, size.y, Mathf.Max(0.0001f, _colliderDepth));
			_collider.center = Vector3.Scale(bounds.center, factor);
		}

		private static Vector3 RelativeScale(Transform from, Transform to)
		{
			var source = from.lossyScale;
			var target = to.lossyScale;

			return new Vector3(Ratio(source.x, target.x), Ratio(source.y, target.y), Ratio(source.z, target.z));
		}

		// A zero somewhere in the chain is an object nothing can be drawn on anyway, so the box collapses
		// with it rather than dividing by it.
		private static float Ratio(float source, float target) => Mathf.Approximately(target, 0f) ? 0f : source / target;

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
