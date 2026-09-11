using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Visual
{
	// The deck lying in the middle of the table. It knows where it is and whose turn each dealt card is —
	// round the table from the seat after the dealer, worked out of the same replicated seats on every
	// client, so nothing about the deal travels on the wire. What the deal looks like, its pace and the
	// way a card flies, is the PokerDealController it is handed: another deal is another controller.
	//
	// Registered rather than serialized, because the hands asking are on spawned players and cannot hold a
	// reference to the table they land at.
	public class PokerDeckVisual : PokerVisual
	{
		[Tooltip("The top of the stack: where a dealt card starts, lying face down.")]
		[SerializeField] private Transform _top;

		[Tooltip("The card drawn as the deck. Only its back is ever shown.")]
		[SerializeField] private PokerCardVisual _stack;

		[SerializeField] private PokerCardDatabase _database;

		[Tooltip("How the deck deals. Swap for another PokerDealController to change the deal's pace or how a card flies.")]
		[Required]
		[SerializeField] private PokerDealController _controller;

		public static PokerDeckVisual Instance { get; private set; }

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void ResetStatics() => Instance = null;

		private void Awake()
		{
			Instance = this;

			if (_stack) _stack.SetCard(CardData.None, false, _database);
		}

		private void OnDestroy()
		{
			if (Instance == this) Instance = null;
		}

		// Holds the card on the deck until its turn comes round. The turn is by seat, not by the order the
		// cards arrived in: a host is told about one player's whole hand before the next player's, and a
		// client may be told in any order at all.
		public void Deal(PokerCardVisual card, int seatIndex, int slot)
		{
			if (!card || !_controller) return;

			card.DealFrom(_top ? _top : transform, _controller.DelayFor(TurnFor(seatIndex, slot)), _controller);
		}

		// Counted among the players in the match only, so an empty chair or a spectator takes no turn.
		private PokerDealTurn TurnFor(int seatIndex, int slot)
		{
			if (!IsBound) return new PokerDealTurn(slot, 0, 1);

			var seats = Mathf.Max(1, Data.ActiveSeatCount.Value, seatIndex + 1);
			var dealer = Data.DealerSeatIndex.Value;
			var mine = SeatsAfterDealer(seatIndex, dealer, seats);

			var dealt = 0;
			var before = 0;

			foreach (var player in GameMode.SeatedPlayers)
			{
				if (!player || !player.Data || !player.Data.InMatch.Value) continue;

				dealt++;
				if (SeatsAfterDealer(player.Data.SeatIndex.Value, dealer, seats) < mine) before++;
			}

			return new PokerDealTurn(slot, before, dealt);
		}

		private static int SeatsAfterDealer(int seatIndex, int dealer, int seats)
			=> ((seatIndex - dealer - 1) % seats + seats) % seats;
	}
}
