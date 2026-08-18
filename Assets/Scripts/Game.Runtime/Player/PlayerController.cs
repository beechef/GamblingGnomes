using System.Collections.Generic;
using Game.Runtime.Controller;
using Unity.Cinemachine;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

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

		// Which bone the look aims depends on what the character is free to do, not on which rig is being
		// rendered — so each rig supplies both, and the mode picks.
		//
		// On their feet, the look drives the chest: everything hanging off it comes along, the neck and the
		// two shoulders alike, so a player looking down lowers their hands with their gaze instead of
		// craning a head off a body that never moved. Anchored in a seat, that is too much — the chair has
		// already decided how the body sits, and swinging the whole torso to read the table would throw the
		// pose away. There the look drives the head alone.
		[Tooltip("Bone the look aims while the character is free to move, on the rig everyone else renders. The chest, so head and arms both follow.")]
		[FormerlySerializedAs("_lookTransform")]
		[FormerlySerializedAs("_pitchTransform")]
		[SerializeField] private Transform _bodyLookTransform;

		[Tooltip("Same bone on the hand-only rig the owner renders.")]
		[FormerlySerializedAs("_ownerLookTransform")]
		[FormerlySerializedAs("_ownerPitchTransform")]
		[SerializeField] private Transform _ownerBodyLookTransform;

		[Tooltip("Bone the look aims once a seat has anchored the body, on the rig everyone else renders. The head alone, so the sitting pose survives.")]
		[SerializeField] private Transform _headLookTransform;

		[Tooltip("Same bone on the hand-only rig the owner renders.")]
		[SerializeField] private Transform _ownerHeadLookTransform;

		private readonly NetworkVariable<float> _pitch = new(0f,
			readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Owner);

		// Standing, yaw turns the whole character and rides along on NetworkTransform. Anchored in a seat,
		// the character stays where the chair put it and the yaw goes to the look bone instead, so that
		// yaw has nothing to ride and needs replicating itself.
		private readonly NetworkVariable<float> _lookYaw = new(0f,
			readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Owner);

		private float _currentPitch;
		private float _lastSentPitch;
		private float _currentLookYaw;
		private float _lastSentLookYaw;
		private float _verticalVelocity;
		private Vector2 _moveInput;
		private bool _sprinting;

		private bool _inputBound;
		private bool _movementEnabled = true;
		private bool _lookConstrained;
		private bool _constraintAllowsRotation;
		private bool _bodyAnchored;
		private PlayerLookMode _seatLookMode;
		private PlayerLookMode _overrideLookMode;
		private bool _hasLookModeOverride;
		private bool _lookSuspended;
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
		private readonly List<AngleSample> _lookYawHistory = new();

		// Owner sees the hand-only rig, everyone else sees the full body rig, so each side only
		// ever drives the bone and camera belonging to the rig it actually renders.
		private CinemachineCamera ActiveCamera => IsOwner ? _ownerFirstPersonCamera : _firstPersonCamera;

		// An override outranks the seat's answer and puts itself back when it is done, so whatever set it
		// never has to know what the seat had decided — and cannot get it wrong on the way out.
		private PlayerLookMode ActiveLookMode => _hasLookModeOverride ? _overrideLookMode : _seatLookMode;

		// Nothing needs undoing when this answer changes: the Animator rewrites both bones every frame, so
		// the one that stops being driven is back under the clip's control on the very next one.
		private Transform ActiveLookTransform => ActiveLookMode == PlayerLookMode.Head
			? IsOwner ? _ownerHeadLookTransform : _headLookTransform
			: IsOwner ? _ownerBodyLookTransform : _bodyLookTransform;

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

		// Whether the body is being held where it was put — by a chair, or anything else that decided the
		// facing. It is the answer to "where does yaw go": anchored, it turns the look bone, because the
		// character itself must not swivel out of the pose it was placed in.
		//
		// Every peer has to agree on it, since a seated player's head turns on the screens of the people
		// watching them just as much as on their own — so this is set wherever the pose is applied, while
		// the limits below it stay the owner's business alone.
		public void SetBodyAnchored(bool anchored)
		{
			_bodyAnchored = anchored;

			// A chair decides how the body sits, so the look drops to the head alone; back on their feet
			// the whole chest turns again.
			_seatLookMode = anchored ? PlayerLookMode.Head : PlayerLookMode.Body;
		}

		// Which bone the look composes onto, for as long as some act needs a say in it. Separate from being
		// anchored because the two genuinely come apart: an accuser pointing across the table is still held
		// by their chair — yaw still goes to the bone rather than the body — and yet aims from the chest,
		// because the whole torso is in the act. Set on every peer, like the anchor and for the same reason.
		public void SetLookModeOverride(PlayerLookMode mode)
		{
			_hasLookModeOverride = true;
			_overrideLookMode = mode;
		}

		public void ClearLookModeOverride()
		{
			_hasLookModeOverride = false;
		}

		// Hands the look bones over to something else entirely. The neck stretch aims the head where it is
		// going and must not be composed with a look input at the same time: two writers on one bone leave
		// whatever the last one wrote, and on the frame the stretch lets go the look would carry on
		// multiplying into that instead of into the pose the Animator meant.
		//
		// Set on every peer, like the anchor and the look mode, because the act it belongs to is replicated.
		public void SetLookSuspended(bool suspended)
		{
			_lookSuspended = suspended;
		}

		// Sitting, lying down or any other anchored pose narrows what the look input is allowed to do:
		// yaw is measured against the anchor's facing instead of being free, and pitch can be tightened.
		// Only the owner takes input, so only the owner needs this; what bone the result lands on is
		// SetBodyAnchored's business and is decided on every client.
		public void ApplyLookConstraint(float referenceYaw, bool allowRotation, Vector2 yawLimits,
			Vector2 pitchLimits)
		{
			_lookConstrained = true;
			_constraintAllowsRotation = allowRotation;
			_constraintYaw = referenceYaw;
			_constraintYawLimits = yawLimits;
			_activePitchLimits = pitchLimits;

			_currentPitch = Mathf.Clamp(_currentPitch, _activePitchLimits.x, _activePitchLimits.y);
			_pitch.Value = _currentPitch;
			_lastSentPitch = _currentPitch;

			// Starts square with the seat: whatever the body was turned toward on the way in is not where
			// sitting down should leave it.
			_currentLookYaw = 0f;
			_lookYaw.Value = 0f;
			_lastSentLookYaw = 0f;
		}

		public void ClearLookConstraint()
		{
			_lookConstrained = false;
			_constraintAllowsRotation = true;
			_activePitchLimits = _pitchLimits;

			// Standing up must not leave the torso still turned — from here yaw is the character's again.
			_currentLookYaw = 0f;
			_lookYaw.Value = 0f;
			_lastSentLookYaw = 0f;
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
			AlignCameraToBody();

			_currentPitch = _pitch.Value;
			_currentLookYaw = _lookYaw.Value;
			ApplyLook(_currentLookYaw, _currentPitch);

			if (!IsOwner)
			{
				PushSample(_pitchHistory, _pitch.Value);
				PushSample(_lookYawHistory, _lookYaw.Value);

				_pitch.OnValueChanged += OnPitchChanged;
				_lookYaw.OnValueChanged += OnLookYawChanged;
				return;
			}

			// Stays disabled in the prefab so it can't overwrite the spawn position the server
			// passed to InstantiateAndSpawn, and only the owner ever drives movement with it.
			_characterController.enabled = true;

			ActiveCamera.enabled = true;

			_lastSentPitch = _currentPitch;
			_lastSentLookYaw = _currentLookYaw;

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
			_lookYaw.OnValueChanged -= OnLookYawChanged;

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

			// Anchored in a seat, the character stays where it was put and the yaw goes to the look bone;
			// free on their feet, yaw turns the whole character, so it rides along on NetworkTransform
			// rather than needing its own NetworkVariable like pitch does.
			if (_bodyAnchored) _currentLookYaw = ConstrainLookYaw(yawDelta);
			else transform.Rotate(Vector3.up, ConstrainYawDelta(yawDelta));

			_currentPitch = Mathf.Clamp(_currentPitch + filteredY * _lookSensitivityY * -1f,
				_activePitchLimits.x, _activePitchLimits.y);

			if (Mathf.Abs(Mathf.DeltaAngle(_lastSentPitch, _currentPitch)) >= _minNetworkSendDeltaDegrees)
			{
				_pitch.Value = _currentPitch;
				_lastSentPitch = _currentPitch;
			}

			if (Mathf.Abs(Mathf.DeltaAngle(_lastSentLookYaw, _currentLookYaw)) >= _minNetworkSendDeltaDegrees)
			{
				_lookYaw.Value = _currentLookYaw;
				_lastSentLookYaw = _currentLookYaw;
			}
		}

		// The look bone answers to the same two settings the character does: a pose that forbids turning
		// forbids turning the head as well — a locked-in chair means facing one way, not facing one way
		// with a free neck — and the limits are read straight off the pose's forward, which is where the
		// body starts from.
		private float ConstrainLookYaw(float yawDelta)
		{
			if (!_constraintAllowsRotation) return 0f;

			return Mathf.Clamp(_currentLookYaw + yawDelta, _constraintYawLimits.x, _constraintYawLimits.y);
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

		// The aim is written here rather than where the input arrives because the Animator poses the
		// skeleton during the Update phase and would otherwise overwrite the bone.
		private void LateUpdate()
		{
			if (!IsSpawned) return;

			if (IsOwner)
			{
				ApplyLook(_currentLookYaw, _currentPitch);
				return;
			}

			var renderTime = Time.timeAsDouble - _interpolationDelay;

			ApplyLook(
				SampleHistory(_lookYawHistory, renderTime, _lookYaw.Value, _maxInterpolationTime),
				SampleHistory(_pitchHistory, renderTime, _pitch.Value, _maxInterpolationTime));
		}

		private void OnPitchChanged(float previous, float current) => PushSample(_pitchHistory, current);
		private void OnLookYawChanged(float previous, float current) => PushSample(_lookYawHistory, current);

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

		// Squares the camera with the character's forward from wherever it hangs, rather than assuming it
		// is parented to the bone the look drives — it is not, and must not be: the neck stretch walks the
		// chain up to whichever bone carries the camera, so the view travels with a head sent across the
		// table. Measured against its own parent, so moving the camera up or down the skeleton needs no
		// change here.
		private void AlignCameraToBody()
		{
			var camera = ActiveCamera;
			if (!camera) return;

			var parent = camera.transform.parent;
			if (!parent) return;

			camera.transform.localRotation = Quaternion.Inverse(parent.rotation) * transform.rotation;
		}

		// Composed on top of whatever the Animator has just written rather than replacing it: every clip in
		// the set poses the chest, so a look that overwrote the bone outright would flatten the sitting
		// pose and leave the character bolt upright at the table. The axes are read fresh each frame for
		// the same reason — the bone's parent is itself animated, so a pair cached at spawn would have the
		// aim drift off level as the torso moves under it.
		//
		// Yaw first, then pitch: pitching inside the turned frame is what makes a body look up along the
		// way it is facing rather than along the way it was placed.
		private void ApplyLook(float yaw, float pitch)
		{
			if (_lookSuspended) return;

			var lookTransform = ActiveLookTransform;
			if (!lookTransform) return;

			var parent = lookTransform.parent;

			var yawAxis = parent ? parent.InverseTransformDirection(transform.up).normalized : Vector3.up;
			var pitchAxis = parent ? parent.InverseTransformDirection(transform.right).normalized : Vector3.right;

			lookTransform.localRotation =
				Quaternion.AngleAxis(yaw, yawAxis)
				* Quaternion.AngleAxis(pitch, pitchAxis)
				* lookTransform.localRotation;
		}
	}
}
