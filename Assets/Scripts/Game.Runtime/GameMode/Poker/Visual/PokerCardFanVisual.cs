using Game.Runtime.Player;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Visual
{
	// The cards a player is holding up, fanned in the hand of whichever rig this client renders.
	//
	// The fan turns about a point *below* the cards, never about each card's own middle. Sliding a card
	// sideways and then rotating it in place splays its top one way and its bottom the other, so the hand
	// opens gaps along the bottom edge while the tops pile up — it reads as skewed tiles, and no amount of
	// tuning the angle fixes it because the shape is wrong. `UIPokerRankingPanel` already learned this on
	// the community row; this is the same arrangement in the world. With the pivot at half a card's height
	// the cards turn about their own bottom edge, which is how a hand held in a fist actually opens.
	public class PokerCardFanVisual : PokerCardGroupVisual
	{
		[Header("Fan")]
		[Tooltip("Degrees between one card and the next. This is the whole spread: the cards separate by turning, not by being slid apart. Negate it to open the fan the other way round — depth is not affected, so the later card stays on top whichever side it opens toward.")]
		[SerializeField] private float _fanAngle = 20f;

		[Tooltip("How far below a card's middle the fan turns. Half a card's height (0.0434) puts the pivot on its bottom edge; further down is a gentler arc that keeps the cards more upright.")]
		[SerializeField] private float _fanRadius = 0.0434f;

		[Header("Hand Bone")]
		[Tooltip("The hand of whichever rig this client renders — the owner's own, or the body everyone else sees.")]
		[SerializeField] private PlayerBone _handBone = PlayerBone.HandLeft;

		[Tooltip("Where the cards sit in the hand, relative to that bone.")]
		[SerializeField] private Vector3 _handLocalPosition = new(0.02f, 0.01f, 0f);

		[SerializeField] private Vector3 _handLocalEuler = new(0f, 90f, 0f);

		[Header("References")]
		[SerializeField] private PlayerRigController _rig;

		private Transform _resolvedAnchor;
		private Transform _anchorBone;

		private void Awake()
		{
			if (!_rig) _rig = GetComponentInParent<PlayerRigController>();
		}

		// Under the hand of whichever rig this client renders — which rig that is stays the rig's business,
		// not this view's. There is no serialized anchor to override it with: one authored in the prefab
		// would name a bone on one rig and be wrong on the other. Parented to the bone, so the cards stay
		// in the hand as it animates.
		//
		// The holder is kept but the *bone* is re-checked every time, because which rig is rendered turns on
		// ownership and ownership is not settled when this object wakes. Resolved once and cached, an early
		// caller pins the fan to the body rig's hand and it never moves — and since the host and a client
		// reach that first call at different moments, it comes out right on one machine and wrong on the
		// other, which is the tell that sends you looking at replication instead of at a cached lookup.
		protected override Transform ResolveAnchor()
		{
			var bone = _rig ? _rig.GetBone(_handBone) : null;
			if (!bone) return _resolvedAnchor ? _resolvedAnchor : transform;

			if (!_resolvedAnchor) _resolvedAnchor = new GameObject("PokerHandAnchor").transform;

			// Moved rather than rebuilt: the cards are parented to this, and a fresh holder would leave them
			// hanging off a corpse.
			if (_anchorBone != bone)
			{
				_anchorBone = bone;

				_resolvedAnchor.SetParent(bone, false);
				_resolvedAnchor.localPosition = _handLocalPosition;
				_resolvedAnchor.localRotation = Quaternion.Euler(_handLocalEuler);
			}

			return _resolvedAnchor;
		}

		// Where a card's middle lands once it has been turned about the shared pivot. The pivot sits
		// _fanRadius below the anchor and the subtraction puts it back, so the middle card is at the
		// anchor's own origin whatever the radius is.
		//
		// Later slots sit further right and nearer the viewer, so each card overlaps the one to its left.
		// The hand anchor is turned about Y, which is why this steps the opposite way to the row lying on
		// the table — the sign is about which way the anchor points, never about which arrangement it is.
		protected override Vector3 SlotPosition(int slot, int count)
		{
			var arm = Vector3.up * _fanRadius;
			var offset = Quaternion.Euler(0f, 0f, Angle(slot, count)) * arm - arm;

			return new Vector3(offset.x, offset.y, slot * DepthStep);
		}

		protected override Quaternion SlotRotation(int slot, int count)
			=> Quaternion.Euler(0f, 0f, Angle(slot, count));

		private float Angle(int slot, int count) => -(slot - (count - 1) * 0.5f) * _fanAngle;
	}
}
