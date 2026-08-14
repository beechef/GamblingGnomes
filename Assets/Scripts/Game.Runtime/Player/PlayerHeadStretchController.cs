using System.Collections.Generic;
using DG.Tweening;
using Unity.Netcode;
using UnityEngine;

namespace Game.Runtime.Player
{
	// A neck that stretches across the table to put a head somewhere it has no business being. The server
	// says who is leaning at whom and for how long; each client plays that out on the rig it renders for
	// that player, so the owner watching from inside their own head and the table watching from outside
	// are watching the same act.
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
		[Tooltip("What the head goes over there to look at. The other player's card hand, by default.")]
		[SerializeField] private PlayerBone _lookBone = PlayerBone.HandRight;

		[Tooltip("How high above the cards the head comes down on them. Level with them shows nothing but their backs; the steeper it looks over the top of the hand, the more of the faces there is to read.")]
		[Range(0f, 89f)]
		[SerializeField] private float _viewPitch = 78f;

		[Tooltip("Which way round the other player the head arrives, measured off their facing. Point it at the side they hold their cards on, so the neck comes in beside them rather than through them.")]
		[Range(-180f, 180f)]
		[SerializeField] private float _viewYaw = 115f;

		[Tooltip("How far the camera ends up from the cards, which is what decides how big they read on screen. The head itself parks nearer than this by however far the camera trails behind the bone.")]
		[SerializeField] private float _viewDistance = 0.55f;

		[Tooltip("Closest the head itself may come, whatever the view distance asks for, so it never ends up inside the hand it went to read.")]
		[SerializeField] private float _headClearance = 0.18f;

		[Tooltip("Longest the neck may get, in world units. Anything further away is leaned toward rather than reached.")]
		[SerializeField] private float _maxReach = 2f;

		[Tooltip("How far the head turns to face what it came to look at, and with it the camera hanging off it. Zero leaves the look input in charge and only the neck travels.")]
		[Range(0f, 1f)]
		[SerializeField] private float _lookWeight = 1f;

		[Header("Timing")]
		[SerializeField] private float _extendDuration = 0.6f;
		[SerializeField] private Ease _extendEase = Ease.OutBack;
		[SerializeField] private float _retractDuration = 0.4f;
		[SerializeField] private Ease _retractEase = Ease.InOutSine;

		[Header("References")]
		[SerializeField] private PlayerRigController _rig;

		// Public on purpose: the whole point of sending your neck across the table is that everyone sees
		// you do it. What the lean earns its owner is the private half, and it is kept somewhere else.
		[HideInInspector] public NetworkVariable<NetworkBehaviourReference> Target = new(default,
			readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);

		private readonly List<Transform> _chain = new();
		private readonly List<float> _chainOffsets = new();
		private readonly List<Quaternion> _chainRotations = new();

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
		}

		public void ServerStretchTo(PlayerRigController target, float duration)
		{
			if (!IsServer || !target || target == _rig) return;

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
			if (!IsStretching || !_head || _chain.Count == 0) return;

			var target = TargetRig;
			if (!target)
			{
				// Whoever was being leaned at has left the table — the neck comes back rather than hanging
				// in the air pointing at nothing.
				TweenWeight(0f, _retractDuration, _retractEase);
				return;
			}

			var lookPoint = ResolveLookPoint(target);
			ApplyStretch(ResolveReachPoint(target, lookPoint), lookPoint);
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
			_head = null;

			var rig = _rig ? _rig.RenderedRig : null;
			if (!rig) return;

			// The neck ends in whichever bone the first person camera hangs off, so a head sent across the
			// table takes the view with it rather than leaving the player behind watching themselves.
			var head = _rig.RenderedHead;
			var neck = rig.Get(PlayerBone.Neck);
			if (!head || !neck || !head.IsChildOf(neck) || head == neck) return;

			for (var bone = head.parent; bone; bone = bone.parent)
			{
				_chain.Insert(0, bone);
				if (bone == neck) break;
			}

			_head = head;

			MeasureRestChain();
		}

		// Measured along the chain rather than straight through it, so a neck that happens to be bent
		// while this runs still reports the length it would have if it were not.
		private void MeasureRestChain()
		{
			_restLength = 0f;

			for (var i = 0; i < _chain.Count; i++)
			{
				_chainOffsets.Add(_restLength);
				_chainRotations.Add(Quaternion.identity);

				var next = i + 1 < _chain.Count ? _chain[i + 1] : _head;
				_restLength += Vector3.Distance(_chain[i].position, next.position);
			}

			if (_restLength <= Epsilon) return;

			for (var i = 0; i < _chainOffsets.Count; i++) _chainOffsets[i] /= _restLength;
		}

		private Vector3 ResolveLookPoint(PlayerRigController target)
		{
			var bone = target.GetBone(_lookBone);
			return bone ? bone.position : target.transform.position;
		}

		// Parked on an angle around the other player rather than on the line straight over to them: coming
		// in level reads the backs of the cards, and it is dropping onto them from above that reads faces.
		// The angle is taken off the other player's own facing, so it stays over their shoulder however
		// they are turned.
		private Vector3 ResolveReachPoint(PlayerRigController target, Vector3 lookPoint)
		{
			var direction = target.transform.rotation * (Quaternion.Euler(-_viewPitch, _viewYaw, 0f) * Vector3.forward);

			// The camera trails the head bone, so the distance worth tuning is the one from the camera and
			// the head is parked short of it — never so short that it arrives inside the cards.
			var camera = _rig ? _rig.RenderedCamera : null;
			var trail = camera ? camera.localPosition.magnitude : 0f;

			var point = lookPoint + direction * Mathf.Max(_viewDistance - trail, _headClearance);

			var origin = _chain[0].position;
			var toPoint = point - origin;
			var distance = toPoint.magnitude;
			if (distance <= Epsilon) return point;

			// Never pulled back inside the shoulders on a neighbour close enough that it would.
			return origin + toPoint / distance * Mathf.Clamp(distance, _restLength, _maxReach);
		}

		private void ApplyStretch(Vector3 reachPoint, Vector3 lookPoint)
		{
			var origin = _chain[0].position;
			var restHead = _head.position;
			var headRotation = _head.rotation;

			var desired = Vector3.LerpUnclamped(restHead, reachPoint, _weight);
			var toDesired = desired - origin;
			if (toDesired.sqrMagnitude <= Epsilon * Epsilon) return;

			// One delta rotation for the whole chain, so whatever turn the animation put into the neck
			// rides along instead of being flattened out of it.
			var aim = Quaternion.FromToRotation(restHead - origin, toDesired);
			var direction = toDesired.normalized;
			var length = toDesired.magnitude;

			// Read the animated pose out first: moving a bone drags its children with it, and the rotation
			// they came in with is the one that has to be turned.
			for (var i = 0; i < _chain.Count; i++) _chainRotations[i] = _chain[i].rotation;

			for (var i = 0; i < _chain.Count; i++)
			{
				_chain[i].SetPositionAndRotation(origin + direction * (length * _chainOffsets[i]), aim * _chainRotations[i]);
			}

			// The segments are spread apart rather than scaled up, so the neck grows without the head
			// growing with it. The head then turns to face what it came for — the camera hangs off this
			// bone, so that turn is what actually puts the cards in front of the player.
			_head.SetPositionAndRotation(desired, AimHead(desired, lookPoint, headRotation));
		}

		// Taken twice, because the camera hangs off the head bone rather than sitting on it: once from the
		// bone to find where the camera lands, and again from there, so what ends up pointed at the cards
		// is the camera and not the neck.
		private Quaternion AimHead(Vector3 headPosition, Vector3 lookPoint, Quaternion animated)
		{
			var camera = _rig ? _rig.RenderedCamera : null;

			var weight = Mathf.Clamp01(_weight * _lookWeight);
			if (!camera || weight <= Epsilon) return animated;

			var offset = Quaternion.Inverse(camera.localRotation);

			var aimed = LookFrom(headPosition, lookPoint, offset, animated);
			aimed = LookFrom(headPosition + aimed * camera.localPosition, lookPoint, offset, animated);

			return Quaternion.Slerp(animated, aimed, weight);
		}

		private static Quaternion LookFrom(Vector3 from, Vector3 lookPoint, Quaternion cameraOffset, Quaternion fallback)
		{
			var direction = lookPoint - from;
			if (direction.sqrMagnitude <= Epsilon * Epsilon) return fallback;

			return Quaternion.LookRotation(direction, Vector3.up) * cameraOffset;
		}
	}
}
