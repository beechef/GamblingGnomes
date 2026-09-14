using UnityEngine;

namespace Game.Runtime.Player
{
	// One caller's say in what a bone's scale is. Held by whoever asked for it rather than looked up by a
	// handle every frame: a tween pushing a growth writes straight into this, which is a field write and
	// not a search through everything else standing on the bone.
	//
	// It is live — changing Value or Mode is seen by the next frame — and it stops mattering the moment it
	// is removed. Nothing here decides anything: the controller reads it, and the caller owns it.
	public sealed class PlayerBoneScaleModifier
	{
		public Vector3 Value;
		public PlayerBoneScaleMode Mode;

		internal PlayerBoneScaleController Owner;
		internal Transform Bone;

		// Idempotent, so a caller that removes on both an end and a teardown does not have to remember
		// which of the two ran first.
		public void Remove()
		{
			var owner = Owner;
			Owner = null;

			if (owner) owner.Remove(this);
		}
	}
}
