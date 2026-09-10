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


		[Tooltip("Where this seat's wagered caps are put down on the table. Falls back to the card anchor, which is at least in front of the right chair.")]
		[SerializeField] private Transform _itemAnchor;

		[Tooltip("Where a sitter is looking when they face straight ahead — out across the table, at head height. Authored rather than derived so it can be nudged; left empty, a shot that asks for it simply leaves the look where it is.")]
		[SerializeField] private Transform _aheadAnchor;

		public int SeatIndex => _seatIndex;
		public Transform CardAnchor => _cardAnchor ? _cardAnchor : SitAnchor;

		// Where a stake lands belongs to the chair for the same reason where the cards lie does: it is a spot
		// on the table in front of one player, authored once in the chair prefab and right at every seat.
		public Transform ItemAnchor => _itemAnchor ? _itemAnchor : CardAnchor;

		// No fallback on purpose. The other two resolve to something plausible because a cap or a card has
		// to land somewhere; a gaze does not, and a plausible wrong answer here is a head aimed at the floor
		// with nothing to say why — the same trap the card anchor fell into when it fell back to the feet.
		public Transform AheadAnchor => _aheadAnchor;

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

		// Chairs are handed out, not chosen: the mode seats whoever arrives, so walking up to one
		// offers nothing. The generic seat still knows how to be sat in â PokerGameMode calls SeatServer â
		// and Sandbox keeps the interaction it always had.
		public override bool CanInteract(NetworkBehaviourReference interactor) => false;

		// And nobody leaves. A chair belongs to a player for as long as they are at the table, whether
		// they are still in the hand, broke, or under.
		public override bool CanStand(NetworkBehaviourReference occupant) => false;

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
