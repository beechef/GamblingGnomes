using System.Collections.Generic;
using Game.Runtime.Interaction;
using Unity.Netcode;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker
{
	public class PokerSeat : SeatInteractable
	{
		// Seats announce themselves the way players do, so a table that spawns after its chairs still
		// finds them all — and nothing has to sweep the scene to go looking.
		private static readonly List<PokerSeat> Registry = new();

		public static IReadOnlyList<PokerSeat> All => Registry;

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void ResetStatics() => Registry.Clear();

		[Header("Poker Seat")]
		[SerializeField] private int _seatIndex;

		[Tooltip("Where this seat's hole cards are laid out on the table. Falls back to the sit anchor.")]
		[SerializeField] private Transform _cardAnchor;

		public int SeatIndex => _seatIndex;
		public Transform CardAnchor => _cardAnchor ? _cardAnchor : SitAnchor;

		private PokerGameMode GameMode => PokerGameMode.Instance;

		public override void OnNetworkSpawn()
		{
			base.OnNetworkSpawn();

			if (!Registry.Contains(this)) Registry.Add(this);

			if (GameMode) GameMode.RegisterSeat(this);
		}

		public override void OnNetworkDespawn()
		{
			Registry.Remove(this);

			if (GameMode) GameMode.UnregisterSeat(this);

			base.OnNetworkDespawn();
		}

		public override bool CanInteract(NetworkBehaviourReference interactor)
		{
			if (!base.CanInteract(interactor)) return false;

			// Once the cards are out the table is closed — a late arrival waits for the next hand.
			return !GameMode || !GameMode.IsGameRunning;
		}

		public override bool CanStand(NetworkBehaviourReference occupant)
		{
			if (!GameMode) return base.CanStand(occupant);

			return GameMode.CanLeaveSeat(OccupantClientId);
		}

		protected override void OnOccupantChanged(ulong previousClientId, ulong currentClientId)
		{
			if (!IsServer || !GameMode) return;

			if (currentClientId != NoOccupantClientId)
			{
				GameMode.HandleSeatOccupied(this, currentClientId);
				return;
			}

			if (previousClientId != NoOccupantClientId) GameMode.HandleSeatReleased(this, previousClientId);
		}
	}
}
