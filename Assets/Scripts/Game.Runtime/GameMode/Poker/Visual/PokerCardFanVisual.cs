using Game.Runtime.Player;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Visual
{
	// The cards a player is holding up, fanned in the hand of whichever rig this client renders.
	public class PokerCardFanVisual : PokerCardGroupVisual
	{
		[Header("Fan")]
		[SerializeField] private float _cardSpacing = 0.03f;
		[SerializeField] private float _fanAngle = 8f;

		[Header("Hand Bone")]
		[Tooltip("The hand of whichever rig this client renders — the owner's own, or the body everyone else sees.")]
		[SerializeField] private PlayerBone _handBone = PlayerBone.HandLeft;

		[Tooltip("Where the cards sit in the hand, relative to that bone.")]
		[SerializeField] private Vector3 _handLocalPosition = new(0.02f, 0.01f, 0f);

		[SerializeField] private Vector3 _handLocalEuler = new(0f, 90f, 0f);

		[Header("References")]
		[SerializeField] private PlayerRigController _rig;

		private Transform _resolvedAnchor;

		private void Awake()
		{
			if (!_rig) _rig = GetComponentInParent<PlayerRigController>();
		}

		// Built once under the hand of whichever rig this client renders — which rig that is stays the
		// rig's business, not this view's. There is no serialized anchor to override it with: one authored
		// in the prefab would name a bone on one rig and be wrong on the other. Parented to the bone, so
		// the cards stay in the hand as it animates.
		protected override Transform ResolveAnchor()
		{
			if (_resolvedAnchor) return _resolvedAnchor;

			var bone = _rig ? _rig.GetBone(_handBone) : null;
			if (!bone) return _resolvedAnchor = transform;

			var holder = new GameObject("PokerHandAnchor").transform;
			holder.SetParent(bone, false);
			holder.localPosition = _handLocalPosition;
			holder.localRotation = Quaternion.Euler(_handLocalEuler);

			return _resolvedAnchor = holder;
		}

		// Later slots sit further right and nearer the viewer, so the fan reads the way a hand held up
		// reads: each card overlapping the one to its left. The hand anchor is turned about Y, which is why
		// this steps the opposite way to the row lying on the table — the sign is about which way the
		// anchor points, never about which arrangement it is.
		protected override Vector3 SlotPosition(int slot, int count)
		{
			var offset = (slot - (count - 1) * 0.5f) * _cardSpacing;

			return new Vector3(offset, 0f, slot * DepthStep);
		}

		protected override Quaternion SlotRotation(int slot, int count)
		{
			var offset = (slot - (count - 1) * 0.5f) * _cardSpacing;

			return Quaternion.Euler(0f, 0f, -offset / Mathf.Max(_cardSpacing, 0.0001f) * _fanAngle);
		}
	}
}
