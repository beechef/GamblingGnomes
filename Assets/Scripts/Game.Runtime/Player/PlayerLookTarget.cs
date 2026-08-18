using UnityEngine;

namespace Game.Runtime.Player
{
	// The place on this player that anything leaning in to look should aim at. Authored as a transform in
	// the prefab rather than derived from a bone and an offset: where a head should end up is a thing you
	// want to drag around in the scene view until it looks right, not a number to guess at twice.
	//
	// Always on offer. Whether there is anything worth seeing when you get there is somebody else's
	// business — and it has to be, or the honest and cheating halves of a peek would arrive at different
	// places and the whole bluff would be readable from across the table.
	public class PlayerLookTarget : MonoBehaviour, IPlayerLookPoint
	{
		[Header("References")]
		[Tooltip("Where a neck stretched over here comes to rest its gaze. Empty falls back to this object.")]
		[SerializeField] private Transform _point;

		public Transform Point => _point ? _point : transform;

		public bool TryGetLookPoint(out Vector3 point)
		{
			point = Point.position;

			return true;
		}
	}
}
