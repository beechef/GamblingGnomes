using UnityEngine;

namespace Game.Runtime.Player
{
	// The place on this player that anything leaning in to look should come to, and the way it should be
	// facing once it is there. Authored as a transform in the prefab rather than derived from a bone and a
	// pair of angles: where a head should end up is a thing you want to drag around in the scene view
	// until it looks right, and where it should be pointed is a thing you want to turn until it looks
	// right — not four numbers to guess at and re-guess whenever the rig changes.
	//
	// Always on offer. Whether there is anything worth seeing when you get there is somebody else's
	// business — and it has to be, or the honest and cheating halves of a peek would arrive at different
	// places and the whole bluff would be readable from across the table.
	public class PlayerLookTarget : MonoBehaviour, IPlayerLookPoint
	{
		[Header("References")]
		[Tooltip("Where a neck stretched over here comes to rest, and which way it looks from there. Its blue axis is the gaze. Empty falls back to this object.")]
		[SerializeField] private Transform _point;

		public Transform Point => _point ? _point : transform;

		public bool TryGetLookPose(out Vector3 position, out Quaternion rotation)
		{
			var point = Point;

			position = point.position;
			rotation = point.rotation;

			return true;
		}
	}
}
