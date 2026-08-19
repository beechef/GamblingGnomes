using System.Collections.Generic;
using Game.Runtime.Interaction;
using Game.Runtime.Player;
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

			// A running table is not a closed one. Someone arriving mid-hand sits down and waits: they are
			// dealt in on the next one, and until then they are Waiting rather than in the hand, which every
			// seat-order helper already reads. What being underway does forbid is *leaving*, and that is
			// CanStand's question rather than this one.
			return CanMeetTheStake(interactor);
		}

		// The seat is free to take, but not to a player who cannot cover what the table asks for: they
		// could only ever fold, and they would still fill a chair the table counts as ready to deal. Both
		// halves are the same read — the rules asset is the same on every machine and money is replicated —
		// so the client asking gets the answer the server will give when the request arrives, and the
		// server checks it again anyway because CanInteract is what Interact gates on.
		private bool CanMeetTheStake(NetworkBehaviourReference interactor)
		{
			if (!interactor.TryGet(out NetworkBehaviour behaviour)) return true;

			var wallet = behaviour.GetComponent<PlayerData>();
			if (!wallet) return true;

			var rules = GameMode ? GameMode.Rules : null;
			return wallet.Money.Value >= (rules ? rules.MinimumMoneyToSit : 1);
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
