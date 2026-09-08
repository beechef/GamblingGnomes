using Game.Runtime.GameMode.Poker.Player;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Stages
{
	// One cap each, in turn. A wager here has no size — there is nothing to raise, nothing to call and
	// nothing to be short of — so the only question the street asks is which kind, and the answer rides
	// in the action's amount as an index into the table's mushroom database.
	//
	// Turns rather than everyone at once, because the player after you has seen what you put up: that is
	// the whole reason the order matters and the reason a hand's winner is given a place in it.
	[CreateAssetMenu(fileName = "PokerStage_ItemWager", menuName = "Game/Poker/Stages/Item Wager")]
	public class PokerItemWagerStage : PokerStage
	{
		[Header("Street")]
		[Tooltip("Which of the two wagers this is. The UI routes on it, and only the second offers folding.")]
		[SerializeField] private PokerPhase _phase = PokerPhase.FirstWager;

		[Tooltip("How many caps of the chosen kind go up. The player still chooses only the kind — this is the size of the stake, set by the table rather than by whoever is acting.")]
		[MinValue(1)]
		[SerializeField] private int _itemsPerWager = 1;

		[Tooltip("On, a player may put the cards down instead of wagering. The design gives this to the second wager only.")]
		[SerializeField] private bool _allowFold;

		[Tooltip("On, whoever took the last hand wagers first. Off, the walk starts from the dealer button, which rotates — the fair reading, since wagering first means everyone else sees your cap before choosing theirs.")]
		[SerializeField] private bool _winnerActsFirst = true;

		[Header("Timing")]
		[Tooltip("Seconds a player has to choose. Zero or less runs no clock at all: no bar, no timeout, and the table waits for an answer.")]
		[SerializeField] private float _turnDuration = -1f;

		[Header("References")]
		[Tooltip("Where the hand jumps when everyone but one player has folded.")]
		[SerializeField] private PokerStage _handOverStage;

		private float _turnElapsed;

		public bool AllowFold => _allowFold;
		public int ItemsPerWager => Mathf.Max(1, _itemsPerWager);

		protected override void OnStartStage()
		{
			Data.Phase.Value = _phase;

			// Clears HasActed, which is what "who still owes a cap" is read off. Without it the second
			// wager would find everyone already marked from the first and end before asking anybody.
			PokerTableUtility.ResetRoundBets(Data, GameMode.SeatedPlayers);

			if (CountWagerers() <= 1)
			{
				FinishStreet();
				return;
			}

			BeginNextTurn(FirstActorFromSeat());
		}

		// NextPlayer walks forward from the seat it is given, so the seat handed back here is the one
		// *before* whoever should wager first. With no winner yet — the first hand of a match — the dealer
		// button serves, which is what rotates.
		private int FirstActorFromSeat()
		{
			if (!_winnerActsFirst) return Data.DealerSeatIndex.Value;

			// The table already remembers who took the last hand; the seat is looked up from it rather than
			// mirrored into a second value that could disagree with the first.
			var winner = GameMode.FindSeatedPlayer(Data.LastWinnerClientId.Value);
			if (!winner) return Data.DealerSeatIndex.Value;

			var winnerSeat = winner.Data.SeatIndex.Value;
			if (winnerSeat < 0) return Data.DealerSeatIndex.Value;

			var seatCount = Mathf.Max(1, Data.ActiveSeatCount.Value);
			return (winnerSeat - 1 + seatCount) % seatCount;
		}

		protected override void OnTickStage(float deltaTime)
		{
			// No clock at all rather than a hidden one: the wager is the moment the table is meant to
			// take its time over, so a turn with no duration simply waits. Set a duration and the bar
			// appears and the timeout comes back with it.
			if (_turnDuration <= 0f) return;
			if (Data.CurrentTurnClientId.Value == PokerGameData.NoTurn) return;

			_turnElapsed += deltaTime;
			if (_turnElapsed < _turnDuration) return;

			// A turn on a clock must always end, and there is no polite answer to "which kind" — so the
			// table wagers for them rather than folding somebody who merely went quiet.
			var clientId = Data.CurrentTurnClientId.Value;
			var database = GameMode.ItemDatabase;
			var fallback = database ? database.DrawItemType() : Items.PokerItemDatabase.PlainChip;

			HandleAction(clientId, PokerActionType.Wager, fallback);
		}

		public override bool HandleAction(ulong clientId, PokerActionType action, int amount)
		{
			if (clientId != Data.CurrentTurnClientId.Value) return false;

			var player = GameMode.FindSeatedPlayer(clientId);
			if (!player || !CanWager(player.Data)) return false;

			switch (action)
			{
				case PokerActionType.Wager:
					if (!IsWagerable(amount)) return false;

					// The pot ledger is the only record of who put up what: PokerBetItem already stamps the
					// owner and the kind, and a second copy on the player would be one the deal's own
					// ServerResetForHand wipes halfway through the round.
					for (var i = 0; i < ItemsPerWager; i++)
					{
						PokerTableUtility.WagerItem(Data, player, (byte)amount);
					}
					break;

				case PokerActionType.Fold:
					if (!_allowFold) return false;

					player.ServerFold();
					break;

				default:
					return false;
			}

			player.Data.HasActed.Value = true;
			AdvanceTurn(player.Data.SeatIndex.Value);
			return true;
		}

		// Who this street has a question for. Deliberately not IsInHand: the first wager runs **before**
		// the deal, so nobody is Active yet and a hand-based count reads as "everybody has folded" — which
		// sent the round straight to the reveal, every time, and looped there. Seated and conscious is what
		// is actually being asked; folding is the only thing that takes somebody out of it afterwards.
		private static bool CanWager(PokerPlayerData data) =>
			data && data.IsSeated && data.IsAlive && data.Status.Value != PokerPlayerStatus.Folded;

		private int CountWagerers()
		{
			var count = 0;
			foreach (var player in GameMode.SeatedPlayers)
			{
				if (player && CanWager(player.Data)) count++;
			}

			return count;
		}

		// Asked here so the bar and the server size the offer through the same code: a kind the database
		// does not carry is one the UI can never light and the server will never take.
		public bool IsWagerable(int itemType)
		{
			if (itemType <= 0 || itemType > byte.MaxValue) return false;

			var database = GameMode ? GameMode.ItemDatabase : null;

			// A kind that exists is not necessarily a kind that may be put up: the Colorful cap is only ever
			// handed to somebody, never staked.
			return database && database.TryGetEntry((byte)itemType, out var entry) && entry.Wagerable;
		}

		public override void HandlePlayerLeft(ulong clientId, int seatIndex)
		{
			if (Data.CurrentTurnClientId.Value != clientId) return;

			AdvanceTurn(seatIndex);
		}

		private void AdvanceTurn(int fromSeatIndex)
		{
			if (CountWagerers() <= 1)
			{
				FinishStreet();
				return;
			}

			BeginNextTurn(fromSeatIndex);
		}

		private void BeginNextTurn(int fromSeatIndex)
		{
			var next = PokerTableUtility.NextPlayer(GameMode.SeatedPlayers, fromSeatIndex,
				player => CanWager(player.Data) && !player.Data.HasActed.Value);

			if (next == null)
			{
				FinishStreet();
				return;
			}

			_turnElapsed = 0f;
			GameMode.BeginTurn(next.ClientId, _turnDuration);
		}

		private void FinishStreet()
		{
			GameMode.ClearTurn();

			// Same count as the one that opened the street, for the same reason: before the deal nobody is
			// in a hand, and reading it that way declared the hand over before it had started.
			var handOver = CountWagerers() <= 1;
			FinishStage(handOver ? _handOverStage : null);
		}
	}
}
