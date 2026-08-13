using Game.Runtime.Player;
using Unity.Netcode;
using UnityEngine;

namespace Game.Runtime.Interaction
{
	public class SeatInteractable : InteractableBase
	{
		public const ulong NoOccupantClientId = ulong.MaxValue;

		[Header("Seat")]
		[SerializeField] private Transform _sitAnchor;
		[SerializeField] private Transform _exitAnchor;
		[SerializeField] private SeatPose _pose = SeatPose.Default;

		[HideInInspector] public NetworkVariable<NetworkBehaviourReference> Occupant = new(default,
			readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);

		public SeatPose Pose => _pose;
		public Transform SitAnchor => _sitAnchor ? _sitAnchor : transform;
		public Transform ExitAnchor => _exitAnchor ? _exitAnchor : transform;

		// Cached rather than resolved on demand: a player who disconnects while seated stops resolving,
		// and the seat still needs to know who it was releasing.
		public ulong OccupantClientId { get; private set; } = NoOccupantClientId;

		// A despawned occupant stops resolving, so a player who disconnects while seated frees the
		// seat on its own rather than needing a disconnect hook to clean up after them.
		public bool IsOccupied => Occupant.Value.TryGet(out NetworkBehaviour _);

		public override void OnNetworkSpawn()
		{
			base.OnNetworkSpawn();
			Occupant.OnValueChanged += HandleOccupantChanged;
		}

		public override void OnNetworkDespawn()
		{
			Occupant.OnValueChanged -= HandleOccupantChanged;
			base.OnNetworkDespawn();
		}

		public override bool CanInteract(NetworkBehaviourReference interactor)
		{
			return base.CanInteract(interactor) && !IsOccupied && !IsSeatedElsewhere(interactor);
		}

		// A seated player stands before taking another chair, so no other seat offers itself while they
		// are down. The seat they are in is excluded rather than every seat: it is already occupied by
		// them, and letting it answer for itself keeps this from depending on the occupant check.
		private static bool IsSeatedElsewhere(NetworkBehaviourReference interactor)
		{
			if (!interactor.TryGet(out NetworkBehaviour behaviour)) return false;

			var seatController = behaviour.GetComponent<PlayerSeatController>();
			return seatController && seatController.IsSeated;
		}

		// Asked before a seated player is allowed to stand. The plain seat never says no; a seat tied
		// to a running game does.
		public virtual bool CanStand(NetworkBehaviourReference occupant) => true;

		public void ReleaseServer(NetworkBehaviourReference occupant)
		{
			if (!IsServer) return;
			if (!Occupant.Value.Equals(occupant)) return;

			Occupant.Value = default;
		}

		public void ReleaseIfOccupiedBy(ulong clientId)
		{
			if (!IsServer) return;
			if (OccupantClientId != clientId) return;

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

		protected virtual void OnOccupantChanged(ulong previousClientId, ulong currentClientId) { }

		private void HandleOccupantChanged(NetworkBehaviourReference previous, NetworkBehaviourReference current)
		{
			var previousClientId = OccupantClientId;

			OccupantClientId = current.TryGet(out NetworkBehaviour behaviour)
				? behaviour.OwnerClientId
				: NoOccupantClientId;

			OnOccupantChanged(previousClientId, OccupantClientId);
		}
	}
}
