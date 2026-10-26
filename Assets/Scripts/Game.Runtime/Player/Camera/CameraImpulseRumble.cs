using Game.Runtime.Controller;
using Sirenix.OdinInspector;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Runtime.Player.Camera
{
	// Rumbles the pad in hand with whatever impulse is shaking this camera, read from the impulse manager
	// rather than from whoever raised it, so every shake (PlayerCameraShake's cues or any other source) is
	// felt with its own envelope. Only while this camera is live, so a body watched across the table never
	// rumbles somebody else's hands. The rotation half drives the heavy low motor, the position half the
	// light high one.
	[RequireComponent(typeof(CinemachineCamera))]
	public class CameraImpulseRumble : MonoBehaviour
	{
		[Header("Strength")]
		[Tooltip("Motor speed per metre of impulse offset (high-frequency motor).")]
		[MinValue(0f)]
		[SerializeField] private float _positionGain = 8f;

		[Tooltip("Motor speed per degree of impulse rotation (low-frequency motor).")]
		[MinValue(0f)]
		[SerializeField] private float _rotationGain = 0.15f;

		[PropertyRange(0f, 1f)]
		[SerializeField] private float _maxSpeed = 0.8f;

		[Tooltip("Impulse channels felt (-1 every channel). Keep it covering the camera's CinemachineImpulseListener mask.")]
		[SerializeField] private int _channelMask = -1;

		[Tooltip("Smallest change in motor speed worth sending to the pad.")]
		[MinValue(0f)]
		[SerializeField] private float _sendThreshold = 0.02f;

		private CinemachineCamera _camera;
		private Gamepad _rumbling;
		private float _low;
		private float _high;

		private void Awake()
		{
			_camera = GetComponent<CinemachineCamera>();
		}

		private void OnEnable()
		{
			InputSchemeController.OnDeviceChanged += Stop;
		}

		private void OnDisable()
		{
			InputSchemeController.OnDeviceChanged -= Stop;
			Stop();
		}

		// Continuous work: the impulse is a signal sampled every frame, not a change to wait for.
		private void LateUpdate()
		{
			var manager = CinemachineImpulseManager.Instance;
			if (!InputSchemeController.IsGamepad || !CinemachineCore.IsLive(_camera) ||
			    !manager.GetImpulseAt(transform.position, false, _channelMask, out var position, out var rotation))
			{
				SetSpeeds(0f, 0f);
				return;
			}

			var low = Mathf.Min(_maxSpeed, Quaternion.Angle(Quaternion.identity, rotation) * _rotationGain);
			var high = Mathf.Min(_maxSpeed, position.magnitude * _positionGain);
			SetSpeeds(low, high);
		}

		private void SetSpeeds(float low, float high)
		{
			if (Mathf.Abs(low - _low) < _sendThreshold && Mathf.Abs(high - _high) < _sendThreshold &&
			    (low > 0f || _low == 0f) && (high > 0f || _high == 0f)) return;

			var pad = Gamepad.current;
			if (pad != _rumbling && _rumbling != null && _rumbling.added) _rumbling.SetMotorSpeeds(0f, 0f);

			_low = low;
			_high = high;
			_rumbling = pad;

			if (pad != null) pad.SetMotorSpeeds(low, high);
		}

		// A pad put down mid-shake, or swapped for another, must not keep buzzing.
		private void Stop()
		{
			if (_rumbling != null && _rumbling.added) _rumbling.SetMotorSpeeds(0f, 0f);

			_rumbling = null;
			_low = 0f;
			_high = 0f;
		}
	}
}
