using Game.Runtime.GameMode.Poker.Items;
using Game.Runtime.GameMode.Poker.Player;
using Game.Runtime.Player;
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

		[Tooltip("On, whoever took the last hand wagers first. Off, the walk starts from the first seat.")]
		[SerializeField] private bool _winnerActsFirst = true;

		[Header("Timing")]
		[Tooltip("Seconds a player has to choose. Zero or less runs no clock at all: no bar, no timeout, and the table waits for an answer.")]
		[SerializeField] private float _turnDuration = -1f;

		[Tooltip("Seconds the table holds after somebody wagers before the next player is asked, so the bet gesture and the cap landing in front of them are seen rather than cut off by the next turn opening. Zero passes the turn on immediately.")]
		[MinValue(0f)]
		[SerializeField] private float _resolveDelay = 1f;

		[Header("References")]
		[Tooltip("Where the hand jumps when everyone but one player has folded.")]
		[SerializeField] private PokerStage _handOverStage;

		private float _turnElapsed;

		// A wager has been taken and is being watched. A flag rather than a sentinel seat, because NoSeat is
		// itself a seat this walk can legitimately start from.
		private bool _resolving;
		private int _resolvingFromSeat;
		private float _resolveElapsed;

		public bool AllowFold => _allowFold;
		public int ItemsPerWager => Mathf.Max(1, _itemsPerWager);

		protected override void OnStartStage()
		{
			_resolving = false;
			_resolveElapsed = 0f;

			Data.Phase.Value = _phase;

			// Clears HasActed, which is what "who still owes a cap" is read off. Without it the second
			// wager would find everyone already marked from the first and end before asking anybody.
			foreach (var player in GameMode.SeatedPlayers) player.Data.ServerResetForRound();

			if (CountWagerers() <= 1)
			{
				FinishStreet();
				return;
			}

			BeginNextTurn(FirstActorFromSeat());
		}

		// NextPlayer walks forward from the seat it is given, so the seat handed back here is the one
		// *before* whoever should wager first. With no winner yet — the first hand of a match — the walk
		// starts from the first seat.
		private int FirstActorFromSeat()
		{
			if (!_winnerActsFirst) return PokerPlayerData.NoSeat;

			// The table already remembers who took the last hand; the seat is looked up from it rather than
			// mirrored into a second value that could disagree with the first.
			var winner = GameMode.FindSeatedPlayer(Data.LastWinnerClientId.Value);
			if (!winner) return PokerPlayerData.NoSeat;

			var winnerSeat = winner.Data.SeatIndex.Value;
			if (winnerSeat < 0) return PokerPlayerData.NoSeat;

			var seatCount = Mathf.Max(1, Data.ActiveSeatCount.Value);
			return (winnerSeat - 1 + seatCount) % seatCount;
		}

		protected override void OnTickStage(float deltaTime)
		{
			// Ahead of the turn clock, and not behind its guard: the hold runs while nobody holds a turn, and
			// a street with no clock at all still has wagers to watch.
			if (_resolving)
			{
				_resolveElapsed += deltaTime;
				if (_resolveElapsed < _resolveDelay) return;

				var from = _resolvingFromSeat;
				_resolving = false;
				_resolveElapsed = 0f;

				ResolveAdvance(from);
				return;
			}

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
			var fallback = database ? database.DrawItemType() : PokerItemDatabase.PlainChip;

			HandleAction(clientId, PokerActionType.Wager, (int)fallback);
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
						PokerTableUtility.WagerItem(Data, player, (PokerItemType)amount);
					}

					// The reach for the cap. PokerItemPotVisual puts the staked cap in this hand on the
					// gesture's own frames, so the two start off the same change.
					player.ActionAnimator?.ServerPlay(PlayerActionIds.Bet);
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

		// Who this street has a question for. Deliberately not IsInHand: a wager placed **before** the deal
		// finds nobody Active yet, and a hand-based count reads as "everybody has folded" — which once sent
		// the round straight to the reveal, every time, and looped there. Seated and conscious is what
		// is actually being asked; folding is the only thing that takes somebody out of it afterwards.
		// A body that took a free chair mid-match is neither dealt in nor scored, so it is not asked to stake
		// either — the street would sit waiting on an answer from somebody who is only watching.
		private static bool CanWager(PokerPlayerData data) =>
			data && data.IsSeated && data.InMatch.Value && data.IsAlive && data.Status.Value != PokerPlayerStatus.Folded;

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
			if (!database) return false;

			// A kind that exists is not necessarily a kind that may be put up: the Colorful cap is only ever
			// handed to somebody, never staked.
			return database.TryGetEntry((PokerItemType)itemType, out var entry) && entry.Wagerable;
		}

		public override void HandlePlayerLeft(ulong clientId, int seatIndex)
		{
			if (Data.CurrentTurnClientId.Value != clientId) return;

			AdvanceTurn(seatIndex);
		}

		// The turn is taken off whoever just acted straight away — they have answered, and leaving it on
		// them would let them answer twice — but the next player is not asked until the beat is over, so
		// the bet gesture and the cap landing are watched rather than cut off. Both exits wait: a street
		// that ends on this wager has the same animation to finish.
		private void AdvanceTurn(int fromSeatIndex)
		{
			if (_resolveDelay > 0f)
			{
				GameMode.ClearTurn();

				_resolving = true;
				_resolvingFromSeat = fromSeatIndex;
				_resolveElapsed = 0f;
				return;
			}

			ResolveAdvance(fromSeatIndex);
		}

		private void ResolveAdvance(int fromSeatIndex)
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
