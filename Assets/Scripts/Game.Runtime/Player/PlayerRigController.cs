using Unity.Netcode;
using UnityEngine;

namespace Game.Runtime.Player
{
	// Which of a player's models this client is actually looking at. The owner renders the hand-only rig
	// from inside their own head and everybody else renders the full body, so anything that needs a bone
	// position asks here instead of picking one rig and being right on one machine out of two.
	public class PlayerRigController : NetworkBehaviour
	{
		[Header("Rigs")]
		[SerializeField] private PlayerBoneRig _fullBodyRig;
		[SerializeField] private PlayerBoneRig _handOnlyRig;

		[Header("View")]
		[Tooltip("The first person camera each rig hangs off a bone of. Anything that moves a head moves the bone this is attached to, so the view goes wherever the head goes.")]
		[SerializeField] private Transform _fullBodyCamera;

		[SerializeField] private Transform _handOnlyCamera;

		public PlayerBoneRig FullBodyRig => _fullBodyRig;
		public PlayerBoneRig HandOnlyRig => _handOnlyRig;

		// The same split PlayerVisual switches the renderers on: the owner's own model is the hand-only
		// one, everyone else's is the full body.
		public PlayerBoneRig RenderedRig => IsOwner ? _handOnlyRig : _fullBodyRig;

		public Transform RenderedCamera => IsOwner ? _handOnlyCamera : _fullBodyCamera;

		// The head itself — the bone the mesh is skinned to and the one anything posing a head has to
		// drive. The camera does not hang off it directly: there is a pivot in between (Head_M/Offset/
		// Camera) holding the eye's offset, so taking the camera's parent hands back that pivot instead.
		// Aiming the pivot turns the view and leaves the head where the clip left it, which reads as
		// correct from inside the player's own eyes and as a head that never moves from every other seat
		// at the table. The camera is a descendant either way, so the view still travels with the bone.
		//
		// The camera's parent is only the fallback, for a rig whose head bone is not named in its map.
		public Transform RenderedHead
		{
			get
			{
				var bone = GetBone(PlayerBone.Head);
				if (bone) return bone;

				var camera = RenderedCamera;
				return camera ? camera.parent : null;
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
