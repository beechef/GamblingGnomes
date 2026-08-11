using Game.Runtime.Interaction;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Runtime.Player
{
	public class PlayerSeatController : NetworkBehaviour
	{
		[Header("Animation")]
		[SerializeField] private Animator _bodyAnimator;
		[SerializeField] private Animator _handOnlyAnimator;
		[SerializeField] private string _defaultAnimationState = "_Idle";
		[SerializeField] private float _animationCrossFade = 0.15f;

		[Tooltip("Animator layer the seat poses live on — gestures and other additive layers stay untouched.")]
		[SerializeField] private int _poseLayer;

		[Header("Input Actions")]
		[SerializeField] private InputActionReference _exitSeatAction;

		[Header("References")]
		[SerializeField] private PlayerController _playerController;

		[HideInInspector] public NetworkVariable<NetworkBehaviourReference> CurrentSeat = new(default,
			readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);

		public bool IsSeated => CurrentSeat.Value.TryGet(out SeatInteractable _);

		public SeatInteractable Seat => CurrentSeat.Value.TryGet(out SeatInteractable seat) ? seat : null;

		public override void OnNetworkSpawn()
		{
			CurrentSeat.OnValueChanged += HandleSeatChanged;

			// Late joiners receive the value before subscribing, so the pose is applied once up front.
			if (IsSeated) ApplySeat(Seat);

			if (!IsOwner) return;

			_exitSeatAction.action.Enable();
			_exitSeatAction.action.performed += OnExitSeatPerformed;
		}

		public override void OnNetworkDespawn()
		{
			CurrentSeat.OnValueChanged -= HandleSeatChanged;

			// Releasing the seat directly rather than through StandServer — writing our own
			// NetworkVariable while despawning is too late to replicate anyway.
			if (IsServer)
			{
				var seat = Seat;
				if (seat) seat.ReleaseServer(new NetworkBehaviourReference(this));
			}

			if (!IsOwner) return;

			_exitSeatAction.action.performed -= OnExitSeatPerformed;
			_exitSeatAction.action.Disable();
		}

		public void SitServer(SeatInteractable seat)
		{
			if (!IsServer || !seat) return;
			if (IsSeated) return;

			CurrentSeat.Value = new NetworkBehaviourReference(seat);
		}

		public void StandServer(bool force = false)
		{
			if (!IsServer) return;

			var seat = Seat;
			var selfReference = new NetworkBehaviourReference(this);

			// The seat gets the last word — a poker seat holds its player until the hand is over.
			if (seat && !force && !seat.CanStand(selfReference)) return;

			if (seat) seat.ReleaseServer(selfReference);

			CurrentSeat.Value = default;
		}

		private void OnExitSeatPerformed(InputAction.CallbackContext ctx)
		{
			if (!IsSeated) return;

			StandRPC();
		}

		[Rpc(SendTo.Server)]
		private void StandRPC(RpcParams rpcParams = default)
		{
			if (rpcParams.Receive.SenderClientId != OwnerClientId) return;

			StandServer();
		}

		private void HandleSeatChanged(NetworkBehaviourReference previous, NetworkBehaviourReference current)
		{
			if (current.TryGet(out SeatInteractable seat))
			{
				ApplySeat(seat);
				return;
			}

			previous.TryGet(out SeatInteractable previousSeat);
			ClearSeat(previousSeat);
		}

		private void ApplySeat(SeatInteractable seat)
		{
			var pose = seat.Pose;

			PlayAnimationState(pose.AnimationState);

			if (!IsOwner || !_playerController) return;

			var anchor = seat.SitAnchor;
			_playerController.SetMovementEnabled(false);
			_playerController.Teleport(anchor.position, anchor.rotation);
			_playerController.ApplyLookConstraint(anchor.eulerAngles.y, pose.AllowRotation, pose.YawLimits, pose.PitchLimits);
		}

		private void ClearSeat(SeatInteractable previousSeat)
		{
			PlayAnimationState(_defaultAnimationState);

			if (!IsOwner || !_playerController) return;

			_playerController.ClearLookConstraint();
			_playerController.SetMovementEnabled(true);

			if (!previousSeat) return;

			var exit = previousSeat.ExitAnchor;
			_playerController.Teleport(exit.position, Quaternion.Euler(0f, exit.eulerAngles.y, 0f));
		}

		private void PlayAnimationState(string stateName)
		{
			if (string.IsNullOrEmpty(stateName)) return;

			CrossFade(_bodyAnimator, stateName);
			CrossFade(_handOnlyAnimator, stateName);
		}

		private void CrossFade(Animator animator, string stateName)
		{
			if (!animator || !animator.isActiveAndEnabled) return;
			if (_poseLayer >= animator.layerCount) return;
			if (!animator.HasState(_poseLayer, Animator.StringToHash(stateName))) return;

			animator.CrossFade(stateName, _animationCrossFade, _poseLayer);
		}
	}
}
