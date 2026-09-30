using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.World
{
	// An endless idle motion on a piece of scenery: floating, swaying, turning. Offsets from the pose it was
	// authored in, so the artist places it where it rests and the loop only moves it around that spot.
	// Local and cosmetic — nobody decides it, so nothing replicates.
	public class WorldMotionLoop : MonoBehaviour
	{
		private const float ThirdTurn = Mathf.PI * 2f / 3f;

		[Header("Float")]
		[Tooltip("How far it drifts from its rest position on each local axis, in metres.")]
		[SerializeField] private Vector3 _floatAmplitude = new(0f, 0.25f, 0f);

		[Tooltip("Seconds for one full rise and fall.")]
		[MinValue(0.01f)]
		[SerializeField] private float _floatPeriod = 4f;

		[Header("Sway")]
		[Tooltip("How far it tilts from its rest rotation on each local axis, in degrees. Each axis runs a third of a turn behind the last, so it circles rather than rocks in a line.")]
		[SerializeField] private Vector3 _swayAmplitude = new(4f, 0f, 4f);

		[Tooltip("Seconds for one full sway.")]
		[MinValue(0.01f)]
		[SerializeField] private float _swayPeriod = 6f;

		[Header("Spin")]
		[Tooltip("Degrees per second around its own up axis. 0 keeps it facing where it was authored.")]
		[SerializeField] private float _spinSpeed;

		[Header("Phase")]
		[Tooltip("Start each copy at a random point of the loop, so several side by side do not move in step.")]
		[SerializeField] private bool _randomPhase = true;

		private Vector3 _restPosition;
		private Quaternion _restRotation;
		private float _phase;
		private float _spinAngle;

		private void Awake()
		{
			_restPosition = transform.localPosition;
			_restRotation = transform.localRotation;
			_phase = _randomPhase ? Random.Range(0f, Mathf.PI * 2f) : 0f;
		}

		private void OnDisable()
		{
			transform.localPosition = _restPosition;
			transform.localRotation = _restRotation;
		}

		private void Update()
		{
			var time = Time.time;

			var floatAngle = time * Mathf.PI * 2f / _floatPeriod + _phase;
			var drift = new Vector3(
				_floatAmplitude.x * Mathf.Sin(floatAngle + ThirdTurn),
				_floatAmplitude.y * Mathf.Sin(floatAngle),
				_floatAmplitude.z * Mathf.Sin(floatAngle + ThirdTurn * 2f));

			var swayAngle = time * Mathf.PI * 2f / _swayPeriod + _phase;
			var tilt = new Vector3(
				_swayAmplitude.x * Mathf.Sin(swayAngle),
				_swayAmplitude.y * Mathf.Sin(swayAngle + ThirdTurn),
				_swayAmplitude.z * Mathf.Sin(swayAngle + ThirdTurn * 2f));

			_spinAngle = Mathf.Repeat(_spinAngle + _spinSpeed * Time.deltaTime, 360f);

			transform.localPosition = _restPosition + _restRotation * drift;
			transform.localRotation = _restRotation * Quaternion.AngleAxis(_spinAngle, Vector3.up) * Quaternion.Euler(tilt);
		}
	}
}
