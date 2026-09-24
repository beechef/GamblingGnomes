using Game.Runtime.Player;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Visual
{
	// The cards a player wears on their forehead (Indian Poker), a row facing out for the rest of the table
	// to read. Hung off the head bone, so it follows every nod and every hallucination that scales the head.
	// Hidden on the holder's own screen: they may not read it, and a card there sits right in front of their eye.
	public class PokerCardHeadVisual : PokerCardGroupVisual
	{
		[Header("Row")]
		[Tooltip("Space left between the edges of neighbouring cards on the forehead.")]
		[MinValue(0f)]
		[SerializeField] private float _gap = 0.004f;

		[Tooltip("How much larger than on the table the cards are drawn here, so they read from across the table.")]
		[MinValue(0.1f)]
		[SerializeField] private float _cardScale = 1.5f;

		[Header("Head Bone")]
		[SerializeField] private PlayerBone _headBone = PlayerBone.Head;

		[Tooltip("Where the row sits relative to the head bone. Measured in the seated pose, in Play mode.")]
		[SerializeField] private Vector3 _headLocalPosition = new(0f, 0.12f, 0.1f);

		[Tooltip("The row's turn relative to the head bone; the cards' -Z must point out of the face.")]
		[SerializeField] private Vector3 _headLocalEuler = new(0f, 180f, 0f);

		[Header("References")]
		[SerializeField] private PlayerRigController _rig;

		private Transform _resolvedAnchor;
		private Transform _anchorBone;

		public override bool FollowsHandCues => false;

		private bool IsHolderScreen => _rig && _rig.IsOwner;

		private void Awake()
		{
			if (!_rig) _rig = GetComponentInParent<PlayerRigController>();
		}

		// Re-checked every time for the same reason the fan is: which rig is drawn turns on ownership, which is
		// not settled when this object wakes.
		protected override Transform ResolveAnchor()
		{
			var bone = _rig ? _rig.GetBone(_headBone) : null;
			if (!bone) return _resolvedAnchor ? _resolvedAnchor : transform;

			if (!_resolvedAnchor) _resolvedAnchor = new GameObject("PokerHeadAnchor").transform;

			if (_anchorBone != bone)
			{
				_anchorBone = bone;

				_resolvedAnchor.SetParent(bone, false);
				_resolvedAnchor.localPosition = _headLocalPosition;
				_resolvedAnchor.localRotation = Quaternion.Euler(_headLocalEuler);
				_resolvedAnchor.localScale = Vector3.one * _cardScale;
			}

			return _resolvedAnchor;
		}

		protected override Vector3 SlotPosition(int slot, int count)
		{
			var offset = (slot - (count - 1) * 0.5f) * (CardSize.x + _gap);

			return new Vector3(offset, 0f, -slot * DepthStep);
		}

		protected override void OnCardAdded(PokerCardVisual card)
		{
			if (IsHolderScreen) card.SetConcealed(true);
		}

		protected override void OnCardRemoved(PokerCardVisual card) => card.SetConcealed(false);
	}
}
