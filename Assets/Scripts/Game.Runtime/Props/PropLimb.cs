using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.Props
{
	// How one rigged limb may move without looking broken: the root bone swings like a shoulder or hip, every
	// inner bone bends like an elbow or knee around one axis, and nothing twists along its own length. Lives on
	// the limb prefab, so every prop wearing that limb moves it the same way.
	public class PropLimb : MonoBehaviour
	{
		[Tooltip("Axis, in each bone's own space, that runs along the bone. Swings and bends never turn around it.")]
		[SerializeField] private Vector3 _boneAxis = Vector3.right;

		[Tooltip("Axis, in each inner bone's own space, its joint bends around.")]
		[SerializeField] private Vector3 _hingeAxis = Vector3.forward;

		[Tooltip("How far an inner joint bends from its authored pose, in degrees. One-sided keeps a knee from folding backwards.")]
		[MinMaxSlider(-150f, 150f, true)]
		[SerializeField] private Vector2 _bendRange = new(0f, 70f);

		[Tooltip("Furthest the root bone swings from its authored rotation, in degrees, on both axes across the bone.")]
		[PropertyRange(0f, 90f)]
		[SerializeField] private float _swingAngle = 35f;

		public Quaternion RandomSwing()
		{
			var axis = _boneAxis.normalized;
			var across = Vector3.Cross(axis, Mathf.Abs(axis.y) < 0.9f ? Vector3.up : Vector3.forward).normalized;
			var acrossOther = Vector3.Cross(axis, across);

			return Quaternion.AngleAxis(Random.Range(-_swingAngle, _swingAngle), across)
				* Quaternion.AngleAxis(Random.Range(-_swingAngle, _swingAngle), acrossOther);
		}

		public Quaternion RandomBend() => Quaternion.AngleAxis(Random.Range(_bendRange.x, _bendRange.y), _hingeAxis);
	}
}
