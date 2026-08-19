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
		[Tooltip("What the head goes over there to look at when that player offers no pose of their own. The other player's card hand, by default.")]
		[SerializeField] private PlayerBone _lookBone = PlayerBone.HandRight;

		[Tooltip("Longest the neck may get, in world units. Anything further away is leaned toward rather than reached.")]
		[SerializeField] private float _maxReach = 2f;

		[Tooltip("How far the head turns onto the rotation the pose asks for, and with it the camera hanging off it. Zero leaves the look input in charge and only the neck travels.")]
		[Range(0f, 1f)]
		[SerializeField] private float _lookWeight = 1f;

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

		private readonly List<Transform> _chain = new();
		private readonly List<float> _chainOffsets = new();
		private readonly List<Quaternion> _chainRotations = new();

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
			if (target)
			{
				ResolveReachPose(target, out _lastReachPoint, out _lastReachRotation);
				_hasLastPose = true;
			}
			else
			{
				// The lean is over, or whoever was being leaned at has left the table. It travels home from
				// where it was rather than snapping: the retract tween is only visible if something keeps
				// drawing the neck while it runs.
				TweenWeight(0f, _retractDuration, _retractEase);

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

			for (var i = 0; i < _chain.Count && i < _chainLocalPositions.Count; i++)
			{
				_chain[i].SetLocalPositionAndRotation(_chainLocalPositions[i], _chainLocalRotations[i]);
			}

			_head.SetLocalPositionAndRotation(_headLocalPosition, _headLocalRotation);

			_restored = !IsStretching;
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
			_chainLocalPositions.Clear();
			_chainLocalRotations.Clear();
			_head = null;
			_hasLastPose = false;
			_restored = true;

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
			_headLocalPosition = _head.localPosition;
			_headLocalRotation = _head.localRotation;
			_restored = true;

			for (var i = 0; i < _chain.Count; i++)
			{
				_chainOffsets.Add(_restLength);
				_chainRotations.Add(Quaternion.identity);
				_chainLocalPositions.Add(_chain[i].localPosition);
				_chainLocalRotations.Add(_chain[i].localRotation);

				var next = i + 1 < _chain.Count ? _chain[i + 1] : _head;
				_restLength += Vector3.Distance(_chain[i].position, next.position);
			}

			if (_restLength <= Epsilon) return;

			for (var i = 0; i < _chainOffsets.Count; i++) _chainOffsets[i] /= _restLength;
		}

		// Whatever the other player is holding up to be read outranks the bone. A fist of cards is edge on
		// to everyone but its owner, so a neck that travelled all that way to stare at the bone holding them
		// arrived at the one angle they cannot be read from.
		private bool TryGetOfferedPose(PlayerRigController target, out Vector3 position, out Quaternion rotation)
		{
			if (target.TryGetComponent<IPlayerLookPoint>(out var offered) && offered.TryGetLookPose(out position, out rotation))
			{
				return true;
			}

			position = default;
			rotation = default;

			return false;
		}

		// The pose says where the eye belongs, which is not where the bone belongs: the camera trails the
		// head by a fixed offset, so the bone is placed behind the spot by exactly that much and the pose's
		// rotation is unwound through it. Solved rather than converged on — both the offset and the twist
		// are known, so there is nothing here to iterate toward.
		private void ResolveReachPose(PlayerRigController target, out Vector3 reachPoint, out Quaternion reachRotation)
		{
			if (!TryGetOfferedPose(target, out var eyePosition, out var eyeRotation))
			{
				// Nothing on offer: travel to the bone, aiming at it from wherever the head is standing now.
				var bone = target.GetBone(_lookBone);
				eyePosition = bone ? bone.position : target.transform.position;

				var toBone = eyePosition - _head.position;
				eyeRotation = toBone.sqrMagnitude > Epsilon * Epsilon
					? Quaternion.LookRotation(toBone, Vector3.up)
					: _head.rotation;
			}

			var camera = _rig ? _rig.RenderedCamera : null;

			reachRotation = camera ? eyeRotation * Quaternion.Inverse(camera.localRotation) : eyeRotation;
			reachPoint = camera ? eyePosition - reachRotation * camera.localPosition : eyePosition;

			var origin = _chain[0].position;
			var toPoint = reachPoint - origin;
			var distance = toPoint.magnitude;
			if (distance <= Epsilon) return;

			// Never stretched past what a neck may become, nor pulled back inside the shoulders on a
			// neighbour close enough that it would.
			reachPoint = origin + toPoint / distance * Mathf.Clamp(distance, _restLength, _maxReach);
		}

		private void ApplyStretch(Vector3 reachPoint, Quaternion reachRotation)
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
			// growing with it. The head then turns onto the rotation it was sent for — the camera hangs off
			// this bone, so that turn is what actually puts the cards in front of the player.
			var lookWeight = Mathf.Clamp01(_weight * _lookWeight);

			_head.SetPositionAndRotation(desired, Quaternion.Slerp(headRotation, reachRotation, lookWeight));
		}
	}
}
