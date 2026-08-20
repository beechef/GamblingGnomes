using System.Collections.Generic;
using DG.Tweening;
using Sirenix.OdinInspector;
using Unity.Netcode;
using UnityEngine;

namespace Game.Runtime.Player
{
	// A neck that stretches across the table to put a head somewhere it has no business being. The server
	// says who is leaning at whom and for how long; each client plays that out on the rig it renders for
	// that player, so the owner watching from inside their own head and the table watching from outside
	// are watching the same act.
	//
	// Where it goes and which way it faces once it is there are both read off a pose the other player
	// offers — a transform dragged and turned in their prefab. Nothing here orbits, offsets or aims by
	// angle: the head travels straight to that spot and arrives wearing its rotation.
	//
	// Ordered into the gap between PlayerController, which runs at the default zero, and the Cinemachine
	// brain, which ships at a hundred. After the look input, so the head is aimed from the rotation it
	// just settled on. Before the brain, because the brain samples the camera hanging off this bone in a
	// LateUpdate of its own — and since the Animator puts the bone back at the top of every frame, a write
	// that lands after that sample is not merely a frame late, it is never seen at all.
	[DefaultExecutionOrder(50)]
	public class PlayerHeadStretchController : NetworkBehaviour
	{
		private const float Epsilon = 0.0001f;

		[Header("Reach")]
		[Tooltip("Where the stretch starts from. Every bone between this one and the one the camera hangs off is spread apart to make the neck; picking one further down puts more of the body into the lean.")]
		[SerializeField] private PlayerBone _stretchRootBone = PlayerBone.Neck;

		[Tooltip("Longest the neck may get, in world units. Anything further away is leaned toward rather than reached.")]
		[SerializeField] private float _maxReach = 2f;

		[Tooltip("How far the head turns onto the rotation the pose asks for, and with it the camera hanging off it. Zero leaves the look input in charge and only the neck travels.")]
		[Range(0f, 1f)]
		[SerializeField] private float _lookWeight = 1f;

		[Header("Curve")]
		[Tooltip("How high the neck arches over the straight line to the target, at the top of the arch, in world units. This is the shape control: the neck rises square to that line along a parabola peaking exactly here, so a bigger number sends the head up and over the table rather than through whatever is lying on it. Zero runs it flat. The head still arrives wearing the pose's own rotation, so raising this changes the neck without changing where the eyes end up looking.")]
		[MinValue(0f)]
		[SerializeField] private float _curveHeight = 0.3f;

		[Tooltip("How gently the arch leaves the shoulders, as a fraction of the distance to the target. It only slides the control point along that straight line — how high the neck goes is the height above, and nothing else. Larger holds the rise back and bulges the arch toward the target.")]
		[Range(0f, 1f)]
		[SerializeField] private float _curveOutHandle = 0.4f;

		[Tooltip("The same, at the far end: how gently the arch settles onto the target. Larger bulges the arch back toward the shoulders.")]
		[Range(0f, 1f)]
		[SerializeField] private float _curveInHandle = 0.4f;

		[Header("Debug")]
		[Tooltip("Writes one line per meaningful change saying what this peer is composing onto the stretched head: the baseline the act carried, the angles this peer is drawing with, and whether it is the leaner's own machine. Run it on host and client together — the two lines beside each other name which half is not moving, which no amount of reading the code will.")]
		[SerializeField] private bool _logComposedLook;

		[Header("Timing")]
		[SerializeField] private float _extendDuration = 0.6f;
		[SerializeField] private Ease _extendEase = Ease.OutBack;
		[SerializeField] private float _retractDuration = 0.4f;
		[SerializeField] private Ease _retractEase = Ease.InOutSine;

		[Header("References")]
		[SerializeField] private PlayerRigController _rig;

		[Tooltip("Told to stop aiming the look bones while the neck is out. Two writers on one bone leave whatever the last one wrote, and the head is going where the neck sends it, not where the mouse points.")]
		[SerializeField] private PlayerController _playerController;

		// Public on purpose: the whole point of sending your neck across the table is that everyone sees
		// you do it. What the lean earns its owner is the private half, and it is kept somewhere else.
		[HideInInspector] public NetworkVariable<NetworkBehaviourReference> Target = new(default,
			readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);

		// Where the leaner's look stood when the lean began. Part of the act rather than a note each client
		// takes for itself: the player can still turn their head out there, and that turn is measured off
		// this — so a baseline captured locally, at whatever moment each client's own copy of the stretch
		// got going, is a different zero on every screen and the turn nobody else sees properly.
		[HideInInspector] public NetworkVariable<Vector2> LookBaseline = new(default,
			readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);

		// Enough to keep the arc table honest on a neck's worth of curvature without costing anything worth
		// measuring — it is rebuilt once per frame, over a handful of joints.
		private const int CurveSamples = 24;

		private readonly List<Transform> _chain = new();
		private readonly List<float> _chainOffsets = new();
		private readonly List<Quaternion> _chainRotations = new();
		private readonly List<Vector3> _chainRestPositions = new();
		private readonly List<float> _arcLengths = new();

		private Vector3 _curveStart;
		private Vector3 _curveOut;
		private Vector3 _curveIn;
		private Vector3 _curveEnd;

		// Where each bone sits on its parent when nothing is pulling on it. Cached rather than read back
		// each frame, because the thing that would read it back is the very thing that displaced it — and
		// a skeleton's bone lengths do not change, so one measurement holds for the life of the rig.
		private readonly List<Vector3> _chainLocalPositions = new();
		private readonly List<Quaternion> _chainLocalRotations = new();

		private Vector3 _headLocalPosition;
		private Quaternion _headLocalRotation;
		private Vector3 _lastReachPoint;
		private Quaternion _lastReachRotation;
		private bool _hasLastPose;
		private bool _restored = true;
		private Vector2 _loggedLook;
		private bool _hasLoggedLook;

		private Transform _head;
		private Tween _weightTween;
		private float _weight;
		private float _weightTarget;
		private float _restLength;
		private double _releaseTime;
		private bool _serverStretching;

		public PlayerRigController TargetRig => Target.Value.TryGet(out PlayerRigController rig) ? rig : null;

		public bool IsStretching => _weight > Epsilon;

		public override void OnNetworkSpawn()
		{
			if (!_rig) _rig = NetworkObject.GetComponent<PlayerRigController>();
			if (!_playerController) _playerController = NetworkObject.GetComponent<PlayerController>();

			BuildChain();

			Target.OnValueChanged += HandleTargetChanged;

			// A client joining mid lean is handed the value before it ever changes again, so the pose is
			// read off the state as it stands rather than waited for.
			RefreshStretch();
		}

		public override void OnNetworkDespawn()
		{
			Target.OnValueChanged -= HandleTargetChanged;

			_weightTween?.Kill();
			_weightTween = null;
			_weight = 0f;
			_weightTarget = 0f;
			_serverStretching = false;

			// Despawned mid lean, the look would never be handed back — there is no frame left to do it in.
			if (_playerController) _playerController.SetLookSuspended(false);
		}

		public void ServerStretchTo(PlayerRigController target, float duration)
		{
			if (!IsServer || !target || target == _rig) return;

			// Captured before the act goes out, so it arrives with it and every peer measures the leaner's
			// own turn from the same zero.
			if (_playerController)
			{
				LookBaseline.Value = new Vector2(_playerController.ReplicatedLookYaw, _playerController.ReplicatedLookPitch);
			}

			Target.Value = new NetworkBehaviourReference(target);

			_releaseTime = NetworkManager.ServerTime.Time + Mathf.Max(0f, duration);
			_serverStretching = true;
		}

		public void ServerRelease()
		{
			if (!IsServer) return;

			_serverStretching = false;
			Target.Value = default;
		}

		// Not a poll for a dependency: the lean ends on a clock, and somebody has to notice the moment
		// it does.
		private void Update()
		{
			if (!IsServer || !IsSpawned || !_serverStretching) return;
			if (NetworkManager.ServerTime.Time < _releaseTime) return;

			ServerRelease();
		}

		private void LateUpdate()
		{
			if (!_head || _chain.Count == 0) return;

			// The Animator writes rotations onto these bones and nothing else, so a neck spread apart by
			// moving bones stays spread apart forever — the pose it would be put back to simply is not
			// authored. Putting the offsets back by hand is what ends the lean, and doing it every frame
			// before reading the animated pose is what stops each frame's stretch compounding on the last.
			RestoreChain();

			// The look and the neck both write the head. While the neck has it, the look lets go — and gets
			// it back the moment the weight is home, not the moment the target cleared, or the last frames of
			// the retract would be fought over.
			if (_playerController) _playerController.SetLookSuspended(IsStretching);

			if (!IsStretching)
			{
				_hasLastPose = false;
				return;
			}

			var target = TargetRig;
			if (target && TryResolveReachPose(target, out _lastReachPoint, out _lastReachRotation))
			{
				_hasLastPose = true;
			}
			else
			{
				// The lean is over, whoever was being leaned at has left the table, or they are offering
				// nothing to come and read. It travels home from where it was rather than snapping: the
				// retract tween is only visible if something keeps drawing the neck while it runs.
				if (!target) TweenWeight(0f, _retractDuration, _retractEase);

				if (!_hasLastPose) return;
			}

			ApplyStretch(_lastReachPoint, _lastReachRotation);
		}

		// Only worth doing while something has been displaced, so an idle player is not writing to these
		// transforms every frame for nothing.
		//
		// Rotation is put back as well as position, and for the same reason: whichever of these bones the
		// clips do not animate keeps whatever was last written to it forever. A neck left spread apart is
		// obvious; a head left turned a few degrees is a camera permanently tilted at the table, with
		// nothing on screen to say why. Bones the Animator does drive are written again at the top of the
		// next frame, so putting them back costs them nothing.
		private void RestoreChain()
		{
			if (_restored && !IsStretching) return;

			// Rotation is only put back once the lean is over. While it is running, the animated rotation is
			// what the aim is composed onto and what the weight blends out of — the Animator wrote it during
			// Update and nothing else has touched it, so putting the bind pose back first would throw away
			// the pose the clip is holding and start every blend from a body that is not there.
			var stretching = IsStretching;

			for (var i = 0; i < _chain.Count && i < _chainLocalPositions.Count; i++)
			{
				if (stretching) _chain[i].localPosition = _chainLocalPositions[i];
				else _chain[i].SetLocalPositionAndRotation(_chainLocalPositions[i], _chainLocalRotations[i]);
			}

			if (stretching) _head.localPosition = _headLocalPosition;
			else _head.SetLocalPositionAndRotation(_headLocalPosition, _headLocalRotation);

			_restored = !stretching;
		}

		private void HandleTargetChanged(NetworkBehaviourReference previous, NetworkBehaviourReference current) => RefreshStretch();

		private void RefreshStretch()
		{
			var stretching = TargetRig;

			TweenWeight(stretching ? 1f : 0f,
				stretching ? _extendDuration : _retractDuration,
				stretching ? _extendEase : _retractEase);
		}

		// Asked for the same thing twice, it leaves the tween alone rather than restarting it: a lean that
		// re-decides where it is going every frame would never finish easing anywhere.
		private void TweenWeight(float target, float duration, Ease ease)
		{
			if (Mathf.Approximately(_weightTarget, target)) return;

			_weightTarget = target;

			_weightTween?.Kill();
			_weightTween = null;

			if (duration <= 0f)
			{
				_weight = target;
				return;
			}

			_weightTween = DOVirtual.Float(_weight, target, duration, value => _weight = value)
				.SetEase(ease)
				.OnComplete(() => _weightTween = null);
		}

		// Walked up from the head rather than configured: whatever segments this skeleton puts between the
		// neck and the head are the ones that have to give.
		private void BuildChain()
		{
			_chain.Clear();
			_chainOffsets.Clear();
			_chainRotations.Clear();
			_chainRestPositions.Clear();
			_arcLengths.Clear();
			_chainLocalPositions.Clear();
			_chainLocalRotations.Clear();
			_head = null;
			_hasLastPose = false;
			_restored = true;

			var rig = _rig ? _rig.RenderedRig : null;
			if (!rig) return;

			// The neck ends in the head bone, and the camera rides along because it hangs somewhere below
			// it — a head sent across the table takes the view with it rather than leaving the player
			// behind watching themselves. A camera parented anywhere else is a rig this cannot work on, so
			// it says so rather than leaning and leaving the view at the table.
			var head = _rig.RenderedHead;
			var root = rig.Get(_stretchRootBone);
			if (!head || !root || !head.IsChildOf(root) || head == root) return;

			var camera = _rig.RenderedCamera;
			if (camera && !camera.IsChildOf(head))
			{
				Debug.LogWarning($"[{nameof(PlayerHeadStretchController)}] {name}: the first person camera does " +
					$"not hang off {head.name}, so a lean would send the head across the table and leave the " +
					"view behind.", this);
			}

			for (var bone = head.parent; bone; bone = bone.parent)
			{
				_chain.Insert(0, bone);
				if (bone == root) break;
			}

			_head = head;

			MeasureRestChain();
		}

		// Measured along the chain rather than straight through it, so a neck that happens to be bent
		// while this runs still reports the length it would have if it were not.
		private void MeasureRestChain()
		{
			_restLength = 0f;
			_headLocalPosition = _head.localPosition;
			_headLocalRotation = _head.localRotation;
			_restored = true;

			// Spread evenly along the arch rather than in the proportions the skeleton was built with, and
			// this rig is why: measured on the gnome, its seven neck joints occupy the first 46% of the
			// neck and the single segment from the last of them to the head takes the other 54%. Keeping
			// those proportions puts every joint in the near half of the curve and leaves the whole descent
			// to one bone — a neck that arches beautifully out of the shoulders and then runs three
			// quarters of a metre dead straight into the head, which is exactly what a rod looks like.
			//
			// A stretching neck is not holding its proportions anyway; the segments are being pulled apart,
			// and how far each is pulled is a choice. Sharing the arch equally is the one spacing that
			// leaves no unjointed run wherever a rig happened to put a long bone. The head is the far end
			// at 1, so the joints take the fractions below it.
			for (var i = 0; i < _chain.Count; i++)
			{
				_chainOffsets.Add((float)i / _chain.Count);
				_chainRotations.Add(Quaternion.identity);
				_chainRestPositions.Add(Vector3.zero);
				_chainLocalPositions.Add(_chain[i].localPosition);
				_chainLocalRotations.Add(_chain[i].localRotation);

				var next = i + 1 < _chain.Count ? _chain[i + 1] : _head;
				_restLength += Vector3.Distance(_chain[i].position, next.position);
			}
		}

		// Where the other player is holding something up to be read, said as a pose in their own prefab. No
		// bone fallback: a fist of cards is edge on to everyone but its owner, so a neck that travelled all
		// that way to stare at the bone holding them arrived at the one angle they cannot be read from —
		// there is no useful answer to guess at when nothing is offered, only a lean that does not happen.
		//
		// The pose says where the eye belongs, which is not where the bone belongs: the camera trails the
		// head by a fixed offset, so the bone is placed behind the spot by exactly that much and the pose's
		// rotation is unwound through it. Solved rather than converged on — both the offset and the twist
		// are known, so there is nothing here to iterate toward.
		private bool TryResolveReachPose(PlayerRigController target, out Vector3 reachPoint, out Quaternion reachRotation)
		{
			reachPoint = default;
			reachRotation = default;

			if (!target.TryGetComponent<IPlayerLookPoint>(out var offered)) return false;
			if (!offered.TryGetLookPose(out var eyePosition, out var eyeRotation)) return false;

			var camera = _rig ? _rig.RenderedCamera : null;

			if (camera)
			{
				// Measured against the head bone, not against the camera's own parent. The camera hangs off a
				// pivot below the bone, so its local transform says where it sits on that pivot and nothing
				// about where it sits on the head — unwinding through it aims the pivot at the cards and
				// leaves the head itself pointing wherever the curve happened to be going.
				//
				// Read live rather than cached because the squaring AlignCameraToBody bakes into the camera
				// lands at spawn, in no order this component may assume. Rotation-only rather than
				// InverseTransformPoint, so the offset survives a rig that is not at unit scale.
				var cameraRotation = Quaternion.Inverse(_head.rotation) * camera.rotation;
				var cameraOffset = Quaternion.Inverse(_head.rotation) * (camera.position - _head.position);

				reachRotation = eyeRotation * Quaternion.Inverse(cameraRotation);
				reachPoint = eyePosition - reachRotation * cameraOffset;
			}
			else
			{
				reachRotation = eyeRotation;
				reachPoint = eyePosition;
			}

			var origin = _chain[0].position;
			var toPoint = reachPoint - origin;
			var distance = toPoint.magnitude;
			if (distance <= Epsilon) return true;

			// Never stretched past what a neck may become, nor pulled back inside the shoulders on a
			// neighbour close enough that it would.
			reachPoint = origin + toPoint / distance * Mathf.Clamp(distance, _restLength, _maxReach);
			return true;
		}

		private void ApplyStretch(Vector3 reachPoint, Quaternion reachRotation)
		{
			var origin = _chain[0].position;

			// Read the animated pose out first: moving a bone drags its children with it, so once the first
			// one is written the rest no longer report where the animation put them. This is also what the
			// weight blends back toward, which is why rest is read every frame rather than cached.
			for (var i = 0; i < _chain.Count; i++)
			{
				_chainRestPositions[i] = _chain[i].position;
				_chainRotations[i] = _chain[i].rotation;
			}

			var restHead = _head.position;
			var headRotation = _head.rotation;

			var restDirection = restHead - origin;
			if (restDirection.sqrMagnitude <= Epsilon * Epsilon) return;

			restDirection.Normalize();

			BuildCurve(origin, restDirection, reachPoint, reachRotation);

			for (var i = 0; i < _chain.Count; i++)
			{
				var t = CurveParameterAtArc(_chainOffsets[i]);

				// Every joint gets its own aim off the tangent where it actually sits, rather than the whole
				// chain sharing one — that difference is the entire reason this reads as a neck bending and
				// not as a row of beads on a wire. The animated rotation is still what is turned, so the
				// twist the clip put into the neck rides along instead of being flattened out.
				var aim = Quaternion.FromToRotation(restDirection, CurveTangent(t));

				_chain[i].SetPositionAndRotation(
					Vector3.LerpUnclamped(_chainRestPositions[i], CurvePoint(t), _weight),
					Quaternion.SlerpUnclamped(_chainRotations[i], aim * _chainRotations[i], _weight));
			}

			// The segments are spread along the curve rather than scaled up, so the neck grows without the
			// head growing with it. The head then turns onto the rotation it was sent for — the camera hangs
			// off this bone, so that turn is what actually puts the cards in front of the player. The curve
			// arrives along that same rotation, so the last joint is already pointing where the head is
			// about to face and there is no kink at the end of the reach.
			var lookWeight = Mathf.Clamp01(_weight * _lookWeight);
			var desired = Vector3.LerpUnclamped(restHead, reachPoint, _weight);

			_head.SetPositionAndRotation(desired, Quaternion.Slerp(headRotation, reachRotation, lookWeight));

			// The stretch decides where the view goes; the player still decides where they look from there.
			// Given back after the aim rather than left running alongside it, so the two are one write on the
			// bone rather than two fighting — and measured from where the look stood when it was taken, so
			// arriving at the cards does not cost the player their bearings.
			if (!_playerController) return;

			_playerController.ComposeLookOnto(_head, LookBaseline.Value.x, LookBaseline.Value.y);

			LogComposedLook();
		}

		// An arch of an authored height thrown between the shoulders and the reach pose. The rise is square
		// to the straight line joining them and the handles only ease the curve off that line, so exactly
		// one number says how high the neck goes and it says it in world units anybody can picture.
		//
		// The handles are fractions of the straight-line distance, so the same pair describes the same
		// shape whether the target is across the table or next to it; the height is absolute, because a
		// neck arching over a table arches over the same table however far along it the head is going.
		private void BuildCurve(Vector3 origin, Vector3 restDirection, Vector3 reachPoint, Quaternion reachRotation)
		{
			var toPoint = reachPoint - origin;
			var chord = toPoint.magnitude;

			_curveStart = origin;
			_curveEnd = reachPoint;

			if (chord <= Epsilon)
			{
				_curveOut = origin;
				_curveIn = reachPoint;
				BuildArcTable();
				return;
			}

			var chordDirection = toPoint / chord;

			// The arch rises square to the straight line rather than along the player's up, so the height
			// means the same thing whether the reach is flat across the table or up at somebody standing.
			// Falls back to world up only for a reach straight up or down, where there is no such square.
			var up = transform.up;
			var arcUp = up - chordDirection * Vector3.Dot(up, chordDirection);
			arcUp = arcUp.sqrMagnitude <= Epsilon * Epsilon ? Vector3.up : arcUp.normalized;

			// Four thirds, and the factor is the whole reason the number in the inspector can be trusted: a
			// cubic whose two handles are pushed square to the chord by d carries a perpendicular profile of
			// 3t(1-t)d, so d = 4H/3 makes that exactly 4H·t(1-t) — the parabola, peaking at H halfway along.
			// Lifting by H itself would arch to three quarters of what it said.
			var lift = arcUp * (_curveHeight * 4f / 3f);

			// The handles keep only their share along the chord. Their square-on part is a second thing
			// deciding how high the neck rises, and with the rest direction pointing almost straight up out
			// of the shoulders it was much the larger of the two — the arch was whatever the out handle
			// happened to make it and the height barely showed. One of them owns the rise now, and it is the
			// one named after it; these two are left saying how gently the curve eases off the line.
			var outOffset = restDirection * (chord * _curveOutHandle);
			var inOffset = reachRotation * Vector3.forward * (chord * _curveInHandle);

			_curveOut = origin + chordDirection * Vector3.Dot(outOffset, chordDirection) + lift;
			_curveIn = reachPoint - chordDirection * Vector3.Dot(inOffset, chordDirection) + lift;

			BuildArcTable();
		}

		// Sampled because a Bezier's parameter is not its arc length: stepping t evenly bunches the joints
		// wherever the curve bends hardest, which is exactly where a neck must not bunch. The table maps
		// distance travelled back to t, so an even share of the arch really is an even length of neck.
		private void BuildArcTable()
		{
			_arcLengths.Clear();
			_arcLengths.Add(0f);

			var previous = CurvePoint(0f);
			var total = 0f;

			for (var i = 1; i <= CurveSamples; i++)
			{
				var point = CurvePoint((float)i / CurveSamples);
				total += Vector3.Distance(previous, point);
				_arcLengths.Add(total);
				previous = point;
			}

			if (total <= Epsilon) return;

			for (var i = 0; i < _arcLengths.Count; i++) _arcLengths[i] /= total;
		}

		private float CurveParameterAtArc(float arc)
		{
			if (_arcLengths.Count < 2) return arc;

			arc = Mathf.Clamp01(arc);

			for (var i = 1; i < _arcLengths.Count; i++)
			{
				if (_arcLengths[i] < arc) continue;

				var span = _arcLengths[i] - _arcLengths[i - 1];
				var within = span <= Epsilon ? 0f : (arc - _arcLengths[i - 1]) / span;

				return (i - 1 + within) / CurveSamples;
			}

			return 1f;
		}

		private Vector3 CurvePoint(float t)
		{
			var inverse = 1f - t;

			return inverse * inverse * inverse * _curveStart
				+ 3f * inverse * inverse * t * _curveOut
				+ 3f * inverse * t * t * _curveIn
				+ t * t * t * _curveEnd;
		}

		// The derivative, normalized. It degenerates only where two control points coincide, which the
		// handle fractions cannot produce unless the target is already inside the shoulders — and that is
		// the case the reach clamp exists to keep out.
		private Vector3 CurveTangent(float t)
		{
			var inverse = 1f - t;

			var derivative = 3f * inverse * inverse * (_curveOut - _curveStart)
				+ 6f * inverse * t * (_curveIn - _curveOut)
				+ 3f * t * t * (_curveEnd - _curveIn);

			return derivative.sqrMagnitude <= Epsilon * Epsilon
				? (_curveEnd - _curveStart).normalized
				: derivative.normalized;
		}

		// Logged on a change rather than per frame, and a peer that never moves is exactly the case worth
		// reading — so a first line goes out whatever it says.
		private void LogComposedLook()
		{
			if (!_logComposedLook) return;

			var yaw = Mathf.DeltaAngle(LookBaseline.Value.x, _playerController.AppliedLookYaw);
			var pitch = Mathf.DeltaAngle(LookBaseline.Value.y, _playerController.AppliedLookPitch);

			if (_hasLoggedLook && Mathf.Abs(Mathf.DeltaAngle(_loggedLook.x, yaw)) < 0.5f
				&& Mathf.Abs(Mathf.DeltaAngle(_loggedLook.y, pitch)) < 0.5f) return;

			_hasLoggedLook = true;
			_loggedLook = new Vector2(yaw, pitch);

			Debug.Log($"[PlayerHeadStretchController] owner={IsOwner} baseline={LookBaseline.Value} " +
				$"applied=({_playerController.AppliedLookYaw:F1}, {_playerController.AppliedLookPitch:F1}) " +
				$"composed=({yaw:F1}, {pitch:F1}) weight={_weight:F2} head={_head.name}");
		}
	}
}
