using Game.Runtime.Player;
using Unity.Netcode;
using UnityEngine;

namespace Game.Runtime.Interaction
{
	public class SeatInteractable : InteractableBase
	{
		[Header("Seat")]
		[SerializeField] private Transform _sitAnchor;
		[SerializeField] private Transform _exitAnchor;
		[SerializeField] private SeatPose _pose = SeatPose.Default;

		[HideInInspector] public NetworkVariable<NetworkBehaviourReference> Occupant = new(default,
			readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);

		public SeatPose Pose => _pose;
		public Transform SitAnchor => _sitAnchor ? _sitAnchor : transform;
		public Transform ExitAnchor => _exitAnchor ? _exitAnchor : transform;

		// A despawned occupant stops resolving, so a player who disconnects while seated frees the
		// seat on its own rather than needing a disconnect hook to clean up after them.
		public bool IsOccupied => Occupant.Value.TryGet(out NetworkBehaviour _);

		public override bool CanInteract(NetworkBehaviourReference interactor)
		{
			return base.CanInteract(interactor) && !IsOccupied;
		}

		public void ReleaseServer(NetworkBehaviourReference occupant)
		{
			if (!IsServer) return;
			if (!Occupant.Value.Equals(occupant)) return;

			Occupant.Value = default;
		}

		protected override void OnInteractServer(NetworkBehaviourReference interactor)
		{
			if (!interactor.TryGet(out NetworkBehaviour behaviour)) return;

			var seatController = behaviour.GetComponent<PlayerSeatController>();
			if (!seatController || seatController.IsSeated) return;

			Occupant.Value = new NetworkBehaviourReference(seatController);
			seatController.SitServer(this);
		}
	}
}
