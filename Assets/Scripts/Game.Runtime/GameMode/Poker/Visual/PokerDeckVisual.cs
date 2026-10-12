using Game.Runtime.Audio;
using Game.Runtime.GameMode.Poker.Stages;
using Sirenix.OdinInspector;
using Unity.Collections;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Visual
{
	// The deck lying in the middle of the table. It knows where it is and whose turn each dealt card is —
	// round the table in seat order, worked out of the same replicated seats on every
	// client, so nothing about the deal travels on the wire. What the deal looks like, its pace and the
	// way a card flies, is the PokerDealController it is handed: another deal is another controller.
	//
	// Registered rather than serialized, because the hands asking are on spawned players and cannot hold a
	// reference to the table they land at.
	public class PokerDeckVisual : PokerVisual
	{
		[Tooltip("The top of the stack: where a dealt card starts, lying face down.")]
		[SerializeField] private Transform _top;

		[Tooltip("Where a card an item draws into a hand starts: above the table, so it drops down into the hand. Empty: the top of the stack.")]
		[SerializeField] private Transform _itemDrawFrom;

		[Tooltip("The card drawn as the deck. Only its back is ever shown.")]
		[SerializeField] private PokerCardVisual _stack;

		[SerializeField] private PokerCardDatabase _database;

		[Tooltip("How the deck deals. Swap for another PokerDealController to change the deal's pace or how a card flies.")]
		[Required]
		[SerializeField] private PokerDealController _controller;

		[Tooltip("Played on the deck as a deal begins, before the first card leaves. Empty: no shuffle heard.")]
		[SerializeField] private AudioEvent _shuffleSound;

		public static PokerDeckVisual Instance { get; private set; }

		// Where a card an item hands out comes from while that item is being played (a vomit puddle, under the
		// table), instead of _itemDrawFrom. One at a time: only one item resolves at once.
		private Transform _itemDrawOverride;

		public void SetItemDrawOrigin(Transform origin) => _itemDrawOverride = origin;

		public void ClearItemDrawOrigin(Transform origin)
		{
			if (_itemDrawOverride == origin) _itemDrawOverride = null;
		}

		private Transform ItemDrawFrom => _itemDrawOverride ? _itemDrawOverride : _itemDrawFrom ? _itemDrawFrom : _top ? _top : transform;

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

		// Heard only as a deal begins: a player joining mid-deal reads the stage on bind and hears nothing.
		protected override void OnBind() => Data.StageId.OnValueChanged += HandleStageChanged;

		protected override void OnUnbind()
		{
			if (Data) Data.StageId.OnValueChanged -= HandleStageChanged;
		}

		private void HandleStageChanged(FixedString32Bytes previous, FixedString32Bytes current)
		{
			if (!_shuffleSound || !AudioManager.Instance) return;
			if (GameMode.FindStage(current.ToString()) is not PokerDealStage) return;

			AudioManager.Instance.PlayOneShot(_shuffleSound, _top ? _top.position : transform.position);
		}

		// Holds the card on the deck until its turn comes round. The turn is by seat, not by the order the
		// cards arrived in: a host is told about one player's whole hand before the next player's, and a
		// client may be told in any order at all.
		//
		// A card dealt straight into the hand goes out in reverse, rightmost first, the way a pick-up hands
		// cards over: the fan draws each card over the one on its left, so the right card has to be in place
		// before the left one arrives beneath it, or the later card flies in through the one already held.
		public void Deal(PokerCardVisual card, int seatIndex, int slot, bool intoHand = false)
		{
			if (!card || !_controller) return;

			var deal = RunningDeal();

			// A card handed out mid-hand, by an item, takes no turn in any deal: it drops in from above at once.
			if (!deal)
			{
				card.DealFrom(ItemDrawFrom, 0f, _controller);
				return;
			}

			var dealt = deal.HoleCardsPerPlayer;
			var round = intoHand && dealt > slot ? dealt - 1 - slot : slot;

			card.DealFrom(_top ? _top : transform, _controller.DelayFor(TurnFor(seatIndex, round, intoHand)), _controller);
		}

		// The board goes out once every hand is dealt, as one more round of the deal with a card per place on
		// the board — the same sum PokerDealPacing.BoardDelayFor gives the deal stage.
		// The hand size comes from the deal rather than from the hands on screen: a client may be told about
		// the board before the hands, and counting cards that have not arrived would send the board out first.
		public void DealBoard(PokerCardVisual card, int boardIndex)
		{
			if (!card || !_controller) return;

			var deal = RunningDeal();

			// A card laid on the board mid-hand, by an item, takes no turn in any deal either.
			if (!deal)
			{
				card.DealFrom(_itemDrawOverride ? _itemDrawOverride : _top ? _top : transform, 0f, _controller);
				return;
			}

			var holeCardsPerPlayer = deal.HoleCardsPerPlayer;

			var players = 0;

			if (IsBound)
			{
				foreach (var player in GameMode.SeatedPlayers)
				{
					if (player && player.Data && player.Data.InMatch.Value) players++;
				}
			}

			var turn = new PokerDealTurn(holeCardsPerPlayer, boardIndex, players, deal.DealsIntoHand);
			card.DealFrom(_top ? _top : transform, _controller.DelayFor(turn), _controller);
		}

		// Asked of the deal running now rather than counted off the cards on screen, which arrive one by one
		// and in a different order on every machine.
		private PokerDealStage RunningDeal() =>
			IsBound ? GameMode.FindStage(Data.StageId.Value.ToString()) as PokerDealStage : null;

		// Counted among the players in the match only, so an empty chair or a spectator takes no turn.
		private PokerDealTurn TurnFor(int seatIndex, int slot, bool intoHand)
		{
			if (!IsBound) return new PokerDealTurn(slot, 0, 1, intoHand);

			var dealt = 0;
			var before = 0;

			foreach (var player in GameMode.SeatedPlayers)
			{
				if (!player || !player.Data || !player.Data.InMatch.Value) continue;

				dealt++;
				if (player.Data.SeatIndex.Value < seatIndex) before++;
			}

			return new PokerDealTurn(slot, before, dealt, intoHand);
		}
	}
}
