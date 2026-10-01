using UnityEngine;

namespace Game.Runtime.Player
{
	// Turns this bone the way the player's look turns their head, so a piece riding the body looks where the
	// player looks — or away from it, with Invert. Read off the angles the controller applied this frame
	// rather than off the head bone, so the clip's own head motion and the pose it started in stay out of it.
	//
	// The bone is put back to its rest pose in Update and turned in LateUpdate, after PlayerController (order
	// 0) has applied the look and after a soft-bone chain on it has swung, so the turn sits on top of both
	// and never builds up.
	[DefaultExecutionOrder(50)]
	public class PlayerLookFollower : MonoBehaviour
	{
		[Tooltip("On, the bone turns the other way: left when the player looks right, down when they look up.")]
		[SerializeField] private bool _invert;

		[Tooltip("How much of the look the bone takes. 1 turns it as far as the head.")]
		[Range(0f, 2f)]
		[SerializeField] private float _weight = 1f;

		private PlayerController _player;
		private Quaternion _rest;

		private void Awake()
		{
			_player = GetComponentInParent<PlayerController>(true);
			_rest = transform.localRotation;
		}

		private void OnDisable() => transform.localRotation = _rest;

		private void Update() => transform.localRotation = _rest;

		private void LateUpdate()
		{
			if (!_player) return;

			var amount = _invert ? -_weight : _weight;
			var frame = _player.transform;

			// Yaw about the body's up, pitch about its right, the order the head is turned in.
			var turn = Quaternion.AngleAxis(_player.AppliedLookYaw * amount, frame.up)
			           * Quaternion.AngleAxis(_player.AppliedLookPitch * amount, frame.right);

			transform.rotation = turn * transform.rotation;
		}
	}
}
