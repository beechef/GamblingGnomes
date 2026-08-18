using System.Collections.Generic;
using Game.Runtime.Controller;
using Unity.Cinemachine;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Runtime.Player
{
	[RequireComponent(typeof(CharacterController))]
	public class PlayerController : NetworkBehaviour
	{
		[Header("Move")]
		[SerializeField] private float _walkSpeed = 4f;
		[SerializeField] private float _sprintSpeed = 7f;
		[SerializeField] private float _jumpHeight = 1.2f;
		[SerializeField] private float _gravity = -18f;

		[Header("Look")]
		[SerializeField] private float _lookSensitivityX = 1f;
		[SerializeField] private float _lookSensitivityY = 1f;
		[SerializeField] private Vector2 _pitchLimits = new(-60f, 60f);

		[Header("Noise filtering")]
		[Tooltip("Raw input delta below this is treated as sensor noise and ignored entirely — doesn't rotate, doesn't get sent.")]
		[SerializeField] private float _inputDeadzone = 0.001f;

		[Tooltip("Minimum accumulated pitch change (degrees) before writing to the NetworkVariable. Local rotation still applies every frame — this only throttles what gets replicated.")]
		[SerializeField] private float _minNetworkSendDeltaDegrees = 0.02f;

		[Header("Remote smoothing (only applied to non-owner clients)")]
		[Tooltip("How far behind (seconds) the remote playback trails the latest received value. Bigger = smoother but laggier.")]
		[SerializeField] private float _interpolationDelay = 0.1f;

		[SerializeField] private int _maxHistorySamples = 20;

		[Tooltip("Max time gap (seconds) we'll smoothly interpolate across. Beyond it, snap instead of stretching a slow-looking Lerp.")]
		[SerializeField] private float _maxInterpolationTime = 0.5f;

		[Header("Input Actions")]
		[SerializeField] private InputActionReference _moveAction;
		[SerializeField] private InputActionReference _lookAction;
		[SerializeField] private InputActionReference _sprintAction;
		[SerializeField] private InputActionReference _jumpAction;
		[SerializeField] private InputActionReference _toggleCursorAction;

		[Header("References")]
		[SerializeField] private CharacterController _characterController;
		[SerializeField] private CinemachineCamera _firstPersonCamera;
		[SerializeField] private CinemachineCamera _ownerFirstPersonCamera;
		[SerializeField] private Transform _pitchTransform;
		[SerializeField] private Transform _ownerPitchTransform;

		private readonly NetworkVariable<float> _pitch = new(0f,
			readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Owner);

		// Standing, yaw turns the whole character and rides along on NetworkTransform. Seated, the body is
		// anchored and only the head turns, so that yaw has nowhere to ride and needs replicating itself.
		private readonly NetworkVariable<float> _headYaw = new(0f,
			readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Owner);

		private float _currentPitch;
		private float _lastSentPitch;
		private float _currentHeadYaw;
		private float _lastSentHeadYaw;
		private float _verticalVelocity;
		private Vector2 _moveInput;
		private bool _sprinting;

		private bool _inputBound;
		private bool _movementEnabled = true;
		private bool _lookConstrained;
		private bool _constraintAllowsRotation;
		private bool _rotateHeadOnly;
		private float _constraintYaw;
		private Vector2 _constraintYawLimits;
		private Vector2 _activePitchLimits;

		public bool MovementEnabled => _movementEnabled;

		private struct AngleSample
		{
			public double Time;
			public float Value;
		}

		private readonly List<AngleSample> _pitchHistory = new();
		private readonly List<AngleSample> _headYawHistory = new();
		private Quaternion _pitchRestRotation = Quaternion.identity;

		// Bone axes are whatever the rig was authored with — on the gnome rig the head bone's
		// local X runs along the character's up, so pitching around it would yaw the head. Both
		// the rotation axis and the camera's alignment are therefore derived from the rest pose
		// at spawn instead of assuming any particular bone orientation.
		private Vector3 _pitchAxisInParentSpace = Vector3.right;

		// Same reasoning as the pitch axis, for the character's up: seated yaw turns the head around it.
		private Vector3 _yawAxisInParentSpace = Vector3.up;

		// Owner sees the hand-only rig, everyone else sees the full body rig, so each side only
		// ever drives the head bone and camera belonging to the rig it actually renders.
		private CinemachineCamera ActiveCamera => IsOwner ? _ownerFirstPersonCamera : _firstPersonCamera;
		private Transform ActivePitchTransform => IsOwner ? _ownerPitchTransform : _pitchTransform;

		// This controller holds one release among however many are outstanding, so the toggle key frees
		// the cursor without overriding a panel that is also holding it.
		private bool _holdsCursorRelease;

		private void Awake()
		{
			_firstPersonCamera.enabled = false;
			_ownerFirstPersonCamera.enabled = false;

			_activePitchLimits = _pitchLimits;
		}

		public void SetMovementEnabled(bool enabled)
		{
			_movementEnabled = enabled;

			if (enabled) return;

			_moveInput = Vector2.zero;
			_verticalVelocity = 0f;
		}

		// Sitting, lying down or any other anchored pose narrows what the look input is allowed to do:
		// yaw is measured against the anchor's facing instead of being free, and pitch can be tightened.
		// rotateHeadOnly is what a seat asks for: the body has been placed and must stay placed, so the yaw
		// the player asks for turns the neck instead of the character. The yaw limits then read as how far
		// they can crane round, measured from the pose's own facing rather than from world zero.
		public void ApplyLookConstraint(float referenceYaw, bool allowRotation, Vector2 yawLimits,
			Vector2 pitchLimits, bool rotateHeadOnly = false)
		{
			_lookConstrained = true;
			_constraintAllowsRotation = allowRotation;
			_rotateHeadOnly = rotateHeadOnly;
			_constraintYaw = referenceYaw;
			_constraintYawLimits = yawLimits;
			_activePitchLimits = pitchLimits;

			_currentPitch = Mathf.Clamp(_currentPitch, _activePitchLimits.x, _activePitchLimits.y);
			_pitch.Value = _currentPitch;
			_lastSentPitch = _currentPitch;

			// Starts level with the seat: whatever the neck was doing on the way in is not where sitting
			// down should leave it.
			_currentHeadYaw = 0f;
			_headYaw.Value = 0f;
			_lastSentHeadYaw = 0f;
		}

		public void ClearLookConstraint()
		{
			_lookConstrained = false;
			_constraintAllowsRotation = true;
			_rotateHeadOnly = false;
			_activePitchLimits = _pitchLimits;

			// Standing up must not leave the head still turned — from here yaw is the body's again.
			_currentHeadYaw = 0f;
			_headYaw.Value = 0f;
			_lastSentHeadYaw = 0f;
		}

		public void Teleport(Vector3 position, Quaternion rotation)
		{
			var wasEnabled = _characterController.enabled;
			_characterController.enabled = false;

			transform.SetPositionAndRotation(position, rotation);

			_characterController.enabled = wasEnabled;
		}

		public override void OnNetworkSpawn()
		{
			CalibrateToRestPose();

			_currentPitch = _pitch.Value;
			_currentHeadYaw = _headYaw.Value;
			ApplyHead(_currentHeadYaw, _currentPitch);

			if (!IsOwner)
			{
				PushSample(_pitchHistory, _pitch.Value);
				PushSample(_headYawHistory, _headYaw.Value);

				_pitch.OnValueChanged += OnPitchChanged;
				_headYaw.OnValueChanged += OnHeadYawChanged;
				return;
			}

			// Stays disabled in the prefab so it can't overwrite the spawn position the server
			// passed to InstantiateAndSpawn, and only the owner ever drives movement with it.
			_characterController.enabled = true;

			ActiveCamera.enabled = true;

			_lastSentPitch = _currentPitch;
			_lastSentHeadYaw = _currentHeadYaw;

			_moveAction.action.Enable();
			_moveAction.action.performed += OnMovePerformed;
			_moveAction.action.canceled += OnMovePerformed;

			_lookAction.action.Enable();
			_lookAction.action.performed += OnLookPerformed;

			_sprintAction.action.Enable();
			_sprintAction.action.performed += OnSprintPerformed;
			_sprintAction.action.canceled += OnSprintPerformed;

			_jumpAction.action.Enable();
			_jumpAction.action.performed += OnJumpPerformed;

			_toggleCursorAction.action.Enable();
			_toggleCursorAction.action.performed += OnToggleCursorPerformed;

			_inputBound = true;

			CursorController.SetBaseLocked(true);
		}

		public override void OnNetworkDespawn()
		{
			_pitch.OnValueChanged -= OnPitchChanged;
			_headYaw.OnValueChanged -= OnHeadYawChanged;

			// The input actions are one shared asset for the whole process, and ownership can pass to the
			// server as a client leaves — so "am I the owner" is not a safe question to ask on the way
			// out. Only the instance that took the controls hands them back; otherwise a remote player
			// despawning would disable the local player's movement.
			if (!_inputBound) return;

			_inputBound = false;

			_moveAction.action.performed -= OnMovePerformed;
			_moveAction.action.canceled -= OnMovePerformed;
			_moveAction.action.Disable();

			_lookAction.action.performed -= OnLookPerformed;
			_lookAction.action.Disable();

			_sprintAction.action.performed -= OnSprintPerformed;
			_sprintAction.action.canceled -= OnSprintPerformed;
			_sprintAction.action.Disable();

			_jumpAction.action.performed -= OnJumpPerformed;
			_jumpAction.action.Disable();

			_toggleCursorAction.action.performed -= OnToggleCursorPerformed;
			_toggleCursorAction.action.Disable();

			// Hands back this controller's own release before dropping the lock entirely, so the count
			// is square for whoever is still holding one when the next player spawns.
			SetCursorReleased(false);
			CursorController.SetBaseLocked(false);
		}

		private void OnMovePerformed(InputAction.CallbackContext ctx)
		{
			_moveInput = ctx.ReadValue<Vector2>();
		}

		private void OnSprintPerformed(InputAction.CallbackContext ctx)
		{
			_sprinting = ctx.ReadValueAsButton();
		}

		private void OnJumpPerformed(InputAction.CallbackContext ctx)
		{
			if (!_movementEnabled) return;

			if (_characterController.isGrounded)
			{
				_verticalVelocity = Mathf.Sqrt(_jumpHeight * -2f * _gravity);
			}
		}

		private void OnToggleCursorPerformed(InputAction.CallbackContext ctx)
		{
			SetCursorReleased(!_holdsCursorRelease);
		}

		private void SetCursorReleased(bool released)
		{
			if (_holdsCursorRelease == released) return;

			_holdsCursorRelease = released;

			if (released) CursorController.RequestUnlock();
			else CursorController.ReleaseUnlock();
		}

		private void OnLookPerformed(InputAction.CallbackContext ctx)
		{
			if (!CursorController.IsLocked) return;

			var delta = ctx.ReadValue<Vector2>();
			var filteredX = Mathf.Abs(delta.x) < _inputDeadzone ? 0f : delta.x;
			var filteredY = Mathf.Abs(delta.y) < _inputDeadzone ? 0f : delta.y;
			if (filteredX == 0f && filteredY == 0f) return;

			var yawDelta = filteredX * _lookSensitivityX;

			// Anchored in a seat, the body stays where it was put and only the neck turns; free on their
			// feet, yaw turns the whole character, so it rides along on NetworkTransform rather than
			// needing its own NetworkVariable like pitch does.
			if (_rotateHeadOnly) _currentHeadYaw = ConstrainHeadYaw(yawDelta);
			else transform.Rotate(Vector3.up, ConstrainYawDelta(yawDelta));

			_currentPitch = Mathf.Clamp(_currentPitch + filteredY * _lookSensitivityY * -1f,
				_activePitchLimits.x, _activePitchLimits.y);

			if (Mathf.Abs(Mathf.DeltaAngle(_lastSentPitch, _currentPitch)) >= _minNetworkSendDeltaDegrees)
			{
				_pitch.Value = _currentPitch;
				_lastSentPitch = _currentPitch;
			}

			if (Mathf.Abs(Mathf.DeltaAngle(_lastSentHeadYaw, _currentHeadYaw)) >= _minNetworkSendDeltaDegrees)
			{
				_headYaw.Value = _currentHeadYaw;
				_lastSentHeadYaw = _currentHeadYaw;
			}
		}

		// The head answers to the same two settings the body does: a pose that forbids turning forbids
		// craning as well — a locked-in chair means facing one way, not facing one way with a free neck —
		// and the limits are read straight off the pose's forward, which is where the neck starts from.
		private float ConstrainHeadYaw(float yawDelta)
		{
			if (!_constraintAllowsRotation) return 0f;

			return Mathf.Clamp(_currentHeadYaw + yawDelta, _constraintYawLimits.x, _constraintYawLimits.y);
		}

		private float ConstrainYawDelta(float yawDelta)
		{
			if (!_lookConstrained) return yawDelta;
			if (!_constraintAllowsRotation) return 0f;

			var currentOffset = Mathf.DeltaAngle(_constraintYaw, transform.eulerAngles.y);
			var clampedOffset = Mathf.Clamp(currentOffset + yawDelta, _constraintYawLimits.x, _constraintYawLimits.y);

			return clampedOffset - currentOffset;
		}

		private void Update()
		{
			if (!IsOwner || !IsSpawned || !_movementEnabled) return;

			var speed = _sprinting ? _sprintSpeed : _walkSpeed;
			var horizontalMove = (transform.forward * _moveInput.y + transform.right * _moveInput.x).normalized * speed;

			if (_characterController.isGrounded && _verticalVelocity < 0f)
				_verticalVelocity = -2f;

			_verticalVelocity += _gravity * Time.deltaTime;

			var motion = horizontalMove;
			motion.y = _verticalVelocity;
			_characterController.Move(motion * Time.deltaTime);
		}

		// Pitch is written here rather than where the input arrives because the Animator poses the
		// skeleton during the Update phase and would otherwise overwrite the head bone.
		private void LateUpdate()
		{
			if (!IsSpawned) return;

			if (IsOwner)
			{
				ApplyHead(_currentHeadYaw, _currentPitch);
				return;
			}

			var renderTime = Time.timeAsDouble - _interpolationDelay;

			ApplyHead(
				SampleHistory(_headYawHistory, renderTime, _headYaw.Value, _maxInterpolationTime),
				SampleHistory(_pitchHistory, renderTime, _pitch.Value, _maxInterpolationTime));
		}

		private void OnPitchChanged(float previous, float current) => PushSample(_pitchHistory, current);
		private void OnHeadYawChanged(float previous, float current) => PushSample(_headYawHistory, current);

		private void PushSample(List<AngleSample> history, float value)
		{
			history.Add(new AngleSample { Time = Time.timeAsDouble, Value = value });

			var oldestNeeded = Time.timeAsDouble - _interpolationDelay - 0.5;
			var removeCount = 0;
			while (removeCount < history.Count - 1 && history[removeCount].Time < oldestNeeded) removeCount++;
			if (removeCount > 0) history.RemoveRange(0, removeCount);

			while (history.Count > _maxHistorySamples) history.RemoveAt(0);
		}

		// Finds the two samples bracketing renderTime and interpolates between them. Two safety
		// cutoffs, both governed by maxGapSeconds: a stale buffer (connection hiccup) snaps to the
		// live value rather than freezing on old data, and a gap wider than the window snaps to the
		// newer sample rather than stretching a Lerp that would read as an unnaturally slow drift.
		private static float SampleHistory(List<AngleSample> history, double renderTime, float liveValue, float maxGapSeconds)
		{
			var count = history.Count;
			if (count == 0) return liveValue;

			var latest = history[count - 1];
			if (Time.timeAsDouble - latest.Time > maxGapSeconds) return liveValue;

			if (count == 1) return latest.Value;

			if (renderTime <= history[0].Time) return history[0].Value;
			if (renderTime >= latest.Time) return latest.Value;

			for (var i = 0; i < count - 1; i++)
			{
				var a = history[i];
				var b = history[i + 1];
				if (renderTime < a.Time || renderTime > b.Time) continue;

				var span = b.Time - a.Time;
				if (span > maxGapSeconds) return b.Value;

				var t = span > 0.0001 ? (float)((renderTime - a.Time) / span) : 1f;
				return Mathf.LerpAngle(a.Value, b.Value, t);
			}

			return latest.Value;
		}

		private void CalibrateToRestPose()
		{
			var pitchTransform = ActivePitchTransform;
			if (!pitchTransform) return;

			_pitchRestRotation = pitchTransform.localRotation;
			_pitchAxisInParentSpace = pitchTransform.parent
				? pitchTransform.parent.InverseTransformDirection(transform.right).normalized
				: transform.right;

			_yawAxisInParentSpace = pitchTransform.parent
				? pitchTransform.parent.InverseTransformDirection(transform.up).normalized
				: transform.up;

			var camera = ActiveCamera;
			if (camera) camera.transform.localRotation = Quaternion.Inverse(pitchTransform.rotation) * transform.rotation;
		}

		// Yaw first, then pitch: pitching inside the yawed frame is what makes a head look up along the way
		// it is facing rather than along the way the body is.
		private void ApplyHead(float yaw, float pitch)
		{
			var pitchTransform = ActivePitchTransform;
			if (!pitchTransform) return;

			pitchTransform.localRotation =
				Quaternion.AngleAxis(yaw, _yawAxisInParentSpace)
				* Quaternion.AngleAxis(pitch, _pitchAxisInParentSpace)
				* _pitchRestRotation;
		}
	}
}
