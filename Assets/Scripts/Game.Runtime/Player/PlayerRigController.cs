using Unity.Netcode;
using UnityEngine;

namespace Game.Runtime.Player
{
	// Which of a player's models this client is actually looking at. The owner renders the hand-only rig
	// from inside their own head and everybody else renders the full body, so anything that needs a bone
	// position asks here instead of picking one rig and being right on one machine out of two.
	public class PlayerRigController : NetworkBehaviour
	{
		// The feature component of a body something on that body belongs to. Bones live under Models/, and
		// the feature components live on named children of the player root — two branches that never meet,
		// so `GetComponentInParent` from a bone walks straight past every one of them and returns null. It
		// did exactly that for the whole life of the bone scale and material effects, which then skipped in
		// silence: an effect that resolved its targets and did nothing at all.
		//
		// This component is the anchor because it sits on the root itself. Up to the body, then down.
		public static T FindOnBody<T>(Transform from) where T : Component
		{
			if (!from) return null;

			var rig = from.GetComponentInParent<PlayerRigController>(true);

			return rig ? rig.GetComponentInChildren<T>(true) : null;
		}

		[Header("Rigs")]
		[SerializeField] private PlayerBoneRig _fullBodyRig;
		[SerializeField] private PlayerBoneRig _handOnlyRig;

		[Header("Focus")]
		[Tooltip("Where somebody else's head aims when they turn to look at this player — face height, on the full body rig, which is the one everybody else renders. A transform rather than a bone plus an offset in code: this rig's bones are Maya-style, so an offset authored against one is wrong before it is tried. Hung under the chest so it follows whatever pose the chair put them in.")]
		[SerializeField] private Transform _focusPoint;

		[Tooltip("Where this player looks when a shot is about them — a fixed point out in front of their chest, authored on the prefab and hung off no model at all. Only the owner ever reads it, and the rig they render is the hand-only one: a point on the body rig would freeze for them, since that rig is switched off and culled. A point on the root cannot.")]
		[SerializeField] private Transform _selfFocusPoint;

		public Transform FocusPoint => _focusPoint;

		// Aiming your own eye at your own FocusPoint aims it at a point on your own chest a hand's breadth
		// away: the solver has nothing sane to answer with and the head comes out wrenched round, which
		// reads as a broken rig rather than as a shot pointed at itself. A shot about you looks here.
		public Transform SelfFocusPoint => _selfFocusPoint;

		public PlayerBoneRig FullBodyRig => _fullBodyRig;
		public PlayerBoneRig HandOnlyRig => _handOnlyRig;

		// The same split PlayerVisual switches the renderers on: the owner's own model is the hand-only
		// one, everyone else's is the full body.
		public PlayerBoneRig RenderedRig => IsOwner ? _handOnlyRig : _fullBodyRig;

		// The head itself — the bone the mesh is skinned to and the one anything posing a head has to drive.
		public Transform RenderedHead => GetBone(PlayerBone.Head);

		public Transform GetBone(PlayerBone bone)
		{
			var rig = RenderedRig;
			return rig ? rig.Get(bone) : null;
		}

		public bool TryGetBone(PlayerBone bone, out Transform boneTransform)
		{
			var rig = RenderedRig;

			boneTransform = null;
			return rig && rig.TryGet(bone, out boneTransform);
		}

		public Vector3 GetBonePosition(PlayerBone bone)
		{
			var rig = RenderedRig;
			return rig ? rig.GetPosition(bone) : transform.position;
		}
	}
}
