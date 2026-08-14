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

		// The bone the view is really tied to. A head that moves without this moving with it is a head the
		// player is left watching from the outside.
		public Transform RenderedHead
		{
			get
			{
				var camera = RenderedCamera;
				return camera && camera.parent ? camera.parent : GetBone(PlayerBone.Head);
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
