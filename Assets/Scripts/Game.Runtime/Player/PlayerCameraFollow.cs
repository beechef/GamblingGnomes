using UnityEngine;

namespace Game.Runtime.Player
{
	// The first-person camera lives outside the skeleton and faces where the look says, never where a bone
	// happens to be. Animation moves the body, input moves the view: a clip shaking the head is seen shaking
	// by everyone else and never by the player looking out of it, and a view that should move on purpose is
	// a look target or a Cinemachine impulse, not a key on a bone.
	public class PlayerCameraFollow : MonoBehaviour
	{
		[Tooltip("The frame the look's two angles are measured in, so at rest the camera looks along the body. Empty uses this object's parent — the player root.")]
		[SerializeField] private Transform _reference;

		// Where the camera faces with no look applied. What something aiming the look solves against.
		public Quaternion RestingRotation
		{
			get
			{
				var frame = _reference ? _reference : transform.parent;
				return frame ? frame.rotation : Quaternion.identity;
			}
		}

		// Yaw about the frame's up, then pitch about its right — the same composition the look bone is turned by.
		public void Aim(float yaw, float pitch) => transform.rotation = RestingRotation * Quaternion.Euler(pitch, yaw, 0f);
	}
}
