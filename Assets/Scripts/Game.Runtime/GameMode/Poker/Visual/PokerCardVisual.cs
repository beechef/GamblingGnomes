using DG.Tweening;
using UnityEngine;
using UnityEngine.Serialization;

namespace Game.Runtime.GameMode.Poker.Visual
{
	// One thin box with a picture on each side, because a card is a physical object: `Card_Face` samples
	// `_BaseMap` on the face that looks down the card's own -Z and `_BackMap` everywhere else, so a single
	// renderer carries both sides and there is no second quad to z-fight or to sort against the first.
	//
	// How big it is, where it sits and what the hit box measures are all authored in the prefab — every
	// card in the deck is the same size, so none of it is a runtime question, and a runtime that worked it
	// out again would be a second answer to a settled question and the one nobody can look at. This says
	// which picture goes on which side and nothing else.
	public class PokerCardVisual : MonoBehaviour
	{
		[Header("Renderer")]
		[Tooltip("The card itself. A mesh rather than a sprite, because a SpriteRenderer draws one material and nothing else: an effect hanging a second pass on a card would be stored and never rendered.")]
		[FormerlySerializedAs("_frontRenderer")]
		[SerializeField] private MeshRenderer _renderer;

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

		// When the card leaves where it lies — its turn in the deal, or its turn among cards picked up
		// together — and, for a deal, how it travels. Until then every placement and flip waits, so a layout
		// laid out around it the moment it was moved cannot send it off ahead of its turn. The deal is
		// cleared when the card lands.
		private float _departAt;
		private PokerDealController _deal;

		private float DepartWait => Mathf.Max(0f, _departAt - Time.time);

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
		private static readonly int BackMapId = Shader.PropertyToID("_BackMap");

		public CardData Card { get; private set; }
		public bool FaceUp { get; private set; } = true;

		// Whether this card is one the player may reach for right now. Set by the beat that allows picking
		// and cleared when it ends, so a card is not offered outside the moment the rules open for it —
		// the server would refuse the pick anyway, and a card that lifts under the cursor and then does
		// nothing is worse than one that never lifted.
		public bool Pickupable { get; set; }

		// How big this card is, in the units of whatever is laying it out. A group that arranges cards has
		// to know — a fan turns about a point half a card below the middle — and the card is the only thing
		// that does: the mesh, its scale and the hit box are all authored in the prefab. An arrangement
		// carrying its own copy of the number is a second place to change when the deck is resized, and the
		// one that silently keeps the old shape.
		//
		// Measured off the card's mesh, which is Unity's own unit cube, so its lossy scale is its size. The
		// division converts that back out of this card's own scale, which is what the parent applies anyway.
		public Vector2 Size
		{
			get
			{
				var face = _renderer ? _renderer.transform : FlipRoot;
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

		// Puts the card on top of the deck and holds it there for `delay` seconds; whatever places it next
		// travels from here, the way `deal` says, once the wait is over. Called before the card is placed or
		// given its face.
		public void DealFrom(Transform deck, float delay, PokerDealController deal)
		{
			if (!deck) return;

			transform.SetPositionAndRotation(deck.position, deck.rotation);
			_departAt = Time.time + Mathf.Max(0f, delay);
			_deal = deal;
		}

		// Where the card sits, and under what. Reparenting keeps the world pose so the travel starts from
		// wherever the card actually was — a card picked up off the table must not jump to the hand and
		// then animate from there. `delay` holds it where it lies before it sets off, so cards moved
		// together leave one after another instead of flying the same path at the same moment.
		public void PlaceAt(Transform parent, Vector3 localPosition, Quaternion localRotation, bool animate, float delay = 0f)
		{
			// Asked before the kill: a card still waiting its turn or in the air keeps going to wherever it
			// now belongs. A group re-lays every card each time another arrives, and snapping this one
			// there cut its flight short — so cards picked up together all landed at once, on top of
			// each other, rather than one after another.
			var travelling = _moveTween != null && _moveTween.IsActive();

			_moveTween?.Kill();
			_liftTween?.Kill();

			if (transform.parent != parent) transform.SetParent(parent, true);

			if (delay > 0f) _departAt = Mathf.Max(_departAt, Time.time + delay);

			var wait = DepartWait;

			if (!animate && !travelling && wait <= 0f && !_deal)
			{
				_lift = 0f;
				ApplyArtHeight();
				Land(localPosition, localRotation);
				return;
			}

			// The deal's travel when there is one, the card's own arc otherwise. Wrapped in a sequence of the
			// card's rather than given a callback of its own, so a deal is free to hang its own on it.
			var travel = _deal
				? _deal.Travel(transform, localPosition, localRotation)
				: ArcTween(transform, localPosition, localRotation, _moveDuration, _moveEase, _moveArc);

			var sequence = DOTween.Sequence().AppendInterval(wait).Append(travel);

			// A chosen card stands off the table while it waits and comes down over the flight, rather than
			// dropping back onto the wood the moment it is committed and then taking off from there.
			if (_lift > 0f)
			{
				sequence.Join(DOVirtual.Float(_lift, 0f, travel.Duration(), value =>
				{
					_lift = value;
					ApplyArtHeight();
				}));
			}

			_moveTween = sequence.OnComplete(() =>
			{
				_deal = null;
				_lift = 0f;
				ApplyArtHeight();
				Land(localPosition, localRotation);
			});
		}

		private void Land(Vector3 localPosition, Quaternion localRotation)
		{
			transform.localPosition = localPosition;
			transform.localRotation = localRotation;
		}

		// From wherever the card is to the given pose in its parent's space, rising `arc` along the parent's
		// view of world up on the way — the table's own up, rather than the card's facing, because a card
		// being lifted rises off the surface it was lying on. Public so a deal can throw a card the same way
		// with numbers of its own.
		public static Tween ArcTween(Transform card, Vector3 localPosition, Quaternion localRotation, float duration, Ease ease, float arc)
		{
			var parent = card.parent;
			var fromPosition = card.localPosition;
			var fromRotation = card.localRotation;
			var lift = (parent ? parent.InverseTransformVector(Vector3.up) : Vector3.up) * arc;

			return DOVirtual.Float(0f, 1f, duration, t =>
				{
					if (!card) return;

					card.localPosition = Vector3.Lerp(fromPosition, localPosition, t) + lift * Mathf.Sin(t * Mathf.PI);
					card.localRotation = Quaternion.Slerp(fromRotation, localRotation, t);
				})
				.SetEase(ease);
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

			// A hand this client may not see shows its back on both sides rather than a blank face.
			if (_database) Draw(_renderer, faceUp ? _database.GetFace(card) : _database.CardBack, _database.CardBack);

			Flip(faceUp, animateFlip);
		}

		// Both pictures onto the one renderer, and nothing else. How big the card is and what the hit box
		// measures are the same on every card in the deck, so they are authored in the prefab where they
		// can be seen and tuned.
		private static void Draw(MeshRenderer renderer, Sprite front, Sprite back)
		{
			if (!renderer) return;

			// Nothing to show is switched off rather than left holding the last card's face.
			if (!front || !front.texture)
			{
				renderer.enabled = false;
				return;
			}

			renderer.enabled = true;

			_block ??= new MaterialPropertyBlock();

			renderer.GetPropertyBlock(_block);
			_block.SetTexture(BaseMapId, front.texture);
			if (back && back.texture) _block.SetTexture(BackMapId, back.texture);
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
				.SetDelay(DepartWait)
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
