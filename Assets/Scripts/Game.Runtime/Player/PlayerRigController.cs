using System.Collections.Generic;
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

		[Tooltip("Where somebody else at the table aims while watching this player decide: a point out in front of them, on the root. Seen from across it lies on this player; seen from the side it lies toward the table's middle, so the board stays in view.")]
		[SerializeField] private Transform _watchPoint;

		[Tooltip("A shot of this player from outside, for another client to cut to while a moment is about them. Never used by the owner, who stays in their own eyes.")]
		[SerializeField] private Camera.PlayerSpectatorCamera _spectatorCamera;

		[Header("Mounts")]
		[Tooltip("Points authored under a bone of the full body rig (the cigarettes at the mouth, their smoke). On the owner's screen they move onto the same bone of the hand-only rig, keeping their pose, so they turn with the head the owner looks through.")]
		[SerializeField] private List<Transform> _ownerMounts = new();

		public Transform FocusPoint => _focusPoint;

		public Camera.PlayerSpectatorCamera SpectatorCamera => _spectatorCamera;

		// Aiming your own eye at your own FocusPoint aims it at a point on your own chest a hand's breadth
		// away: the solver has nothing sane to answer with and the head comes out wrenched round, which
		// reads as a broken rig rather than as a shot pointed at itself. A shot about you looks here.
		public Transform SelfFocusPoint => _selfFocusPoint;

		// Falls back to the face when the prefab has no watch point.
		public Transform WatchPoint => _watchPoint ? _watchPoint : _focusPoint;

		public PlayerBoneRig FullBodyRig => _fullBodyRig;
		public PlayerBoneRig HandOnlyRig => _handOnlyRig;

		// The same split PlayerVisual switches the renderers on: the owner's own model is the hand-only
		// one, everyone else's is the full body.
		public PlayerBoneRig RenderedRig => IsOwner ? _handOnlyRig : _fullBodyRig;

		// The head itself — the bone the mesh is skinned to and the one anything posing a head has to drive.
		public Transform RenderedHead => GetBone(PlayerBone.Head);

		public override void OnNetworkSpawn()
		{
			base.OnNetworkSpawn();
			if (IsOwner) MoveMounts(_fullBodyRig, _handOnlyRig);
		}

		public override void OnNetworkDespawn()
		{
			if (IsOwner) MoveMounts(_handOnlyRig, _fullBodyRig);
			base.OnNetworkDespawn();
		}

		private void MoveMounts(PlayerBoneRig from, PlayerBoneRig to)
		{
			if (!from || !to) return;

			foreach (var mount in _ownerMounts)
			{
				if (!mount || !from.TryGetBoneOf(mount.parent, out var bone) || !to.TryGet(bone, out var target)) continue;

				mount.SetParent(target, false);
			}
		}

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
