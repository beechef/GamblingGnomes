using Game.Runtime.GameMode.Poker.BetItems;
using Game.Runtime.GameMode.Poker.Player;
using Game.Runtime.Player;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Stages
{
	// Which kind a bet puts up: the player's pick, or one the table draws for them. A table that draws
	// asks only "stay or go", so the bar skips the picker.
	public enum PokerBetKindSelection : byte
	{
		Chosen = 0,
		Random = 1
	}

	// What a turn that runs out of time answers with.
	public enum PokerBetTimeout : byte
	{
		BetRandomKind = 0,
		Fold = 1
	}

	// One cap each, in turn. A bet here has no size — there is nothing to raise, nothing to call and
	// nothing to be short of — so the only question the street asks is which kind, and the answer rides
	// in the action's amount as an index into the table's mushroom database.
	//
	// Turns rather than everyone at once, because the player after you has seen what you put up: that is
	// the whole reason the order matters and the reason a hand's winner is given a place in it.
	[CreateAssetMenu(fileName = "PokerStage_Street", menuName = "Game/Poker/Stages/Street")]
	public class PokerStreetStage : PokerStage
	{
		[Header("Street")]
		[Tooltip("Which of the two streets this is. The UI routes on it, and only the second offers folding.")]
		[SerializeField] private PokerPhase _phase = PokerPhase.FirstStreet;

		[Tooltip("How many caps of the chosen kind go up. The player still chooses only the kind — this is the size of the stake, set by the table rather than by whoever is acting.")]
		[MinValue(1)]
		[SerializeField] private int _stakeSize = 1;

		[Tooltip("Whether the player picks the kind they put up or the table draws one for them.")]
		[SerializeField] private PokerBetKindSelection _kindSelection = PokerBetKindSelection.Chosen;

		[Tooltip("On, a player may put the cards down instead of betting. The design gives this to the second street only.")]
		[SerializeField] private bool _allowFold;

		[Tooltip("On, whoever opens the hand — last hand's winner, or the next player after them — bets first. Off, the walk starts from the first seat.")]
		[SerializeField] private bool _winnerActsFirst = true;

		[Header("Board")]
		[Tooltip("Board cards turned over as the street opens, before anybody is asked. Zero turns none.")]
		[MinValue(0)]
		[SerializeField] private int _communityCardsToReveal;

		[Tooltip("Seconds the table looks at the cards just turned before the first player is asked.")]
		[MinValue(0f)]
		[SerializeField] private float _revealHold = 1f;

		[Header("All In")]
		[Tooltip("On, a player may go all in instead of betting: they stake this street's bet and the cap below, the betting closes, and everyone else answers at once in the all-in stage.")]
		[SerializeField] private bool _allowAllIn;

		[Tooltip("What going all in puts up, on top of this street's bet.")]
		[ShowIf(nameof(_allowAllIn))]
		[SerializeField] private PokerBetItemType _allInBetItemType = PokerBetItemType.Colorful;

		[Tooltip("Where the betting goes once somebody is all in. Must be in the sequence, since the table jumps to it.")]
		[ShowIf(nameof(_allowAllIn))]
		[SerializeField] private PokerStage _allInStage;

		[Header("Timing")]
		[Tooltip("Seconds a player has to choose. Zero or less runs no clock at all: no bar, no timeout, and the table waits for an answer.")]
		[SerializeField] private float _turnDuration = -1f;

		[Tooltip("What a turn that runs out answers with. Wherever folding is refused (this street, an item, a lock) it falls back to a bet of a drawn kind, then to going all in, because a turn on a clock must always end.")]
		[SerializeField] private PokerBetTimeout _timeoutAction = PokerBetTimeout.BetRandomKind;

		[Tooltip("Seconds the table holds after somebody bets before the next player is asked, so the bet gesture and the cap landing in front of them are seen rather than cut off by the next turn opening. Zero passes the turn on immediately.")]
		[MinValue(0f)]
		[SerializeField] private float _resolveDelay = 1f;

		[Header("References")]
		[Tooltip("Where the hand jumps when everyone but one player has folded.")]
		[SerializeField] private PokerStage _handOverStage;

		private float _turnElapsed;

		// A bet has been taken and is being watched. A flag rather than a sentinel seat, because NoSeat is
		// itself a seat this walk can legitimately start from.
		private bool _resolving;
		private int _resolvingFromSeat;
		private float _resolveElapsed;

		// The board just turned is being looked at; the first turn waits for it.
		private bool _holdingReveal;
		private float _revealElapsed;

		private bool _allInCalled;

		public bool AllowAllIn => _allowAllIn && _allInStage;
		public bool PicksKind => _kindSelection == PokerBetKindSelection.Chosen;

		// What a bet here puts up right now: the street's own size, as the table's modules have changed it.
		public int StakeSize => GameMode ? GameMode.ModifyStakeSize(this, Mathf.Max(1, _stakeSize)) : Mathf.Max(1, _stakeSize);

		// The street's own rule, narrowed by whatever the modules forbid. The bar and the server both ask here.
		public bool CanFold(PokerPlayerData player) =>
			_allowFold && GameMode && GameMode.IsActionAllowed(player, PokerActionType.Fold);

		// Whether somebody still in the hand has already put a bet up on this street, so what this player puts
		// up matches it: a call rather than an opening bet. Read off replicated state, for the bar to name the
		// button; the act on the wire is the same Bet either way.
		public bool IsCall(PokerPlayerData player)
		{
			if (!GameMode) return false;

			foreach (var other in GameMode.SeatedPlayers)
			{
				if (!other || other.Data == player) continue;
				if (CanBet(other.Data) && other.Data.HasActed.Value) return true;
			}

			return false;
		}

		protected override void OnStartStage()
		{
			_resolving = false;
			_resolveElapsed = 0f;
			_holdingReveal = false;
			_allInCalled = false;

			Data.Phase.Value = _phase;

			// Clears HasActed, which is what "who still owes a cap" is read off. Without it the second
			// street would find everyone already marked from the first and end before asking anybody.
			foreach (var player in GameMode.SeatedPlayers) player.Data.ServerResetForRound();

			if (CountBettors() <= 1)
			{
				FinishStreet();
				return;
			}

			if (_communityCardsToReveal > 0)
			{
				GameMode.ServerRevealCommunityCards(_communityCardsToReveal);

				if (_revealHold > 0f)
				{
					_holdingReveal = true;
					_revealElapsed = 0f;
					return;
				}
			}

			BeginNextTurn(FirstActorFromSeat());
		}

		// NextPlayer walks forward from the seat it is given, so the seat handed back here is the one
		// *before* whoever should bet first.
		private int FirstActorFromSeat() => _winnerActsFirst ? GameMode.SeatBeforeHandOpener() : PokerPlayerData.NoSeat;

		protected override void OnTickStage(float deltaTime)
		{
			if (_holdingReveal)
			{
				_revealElapsed += deltaTime;
				if (_revealElapsed < _revealHold) return;

				_holdingReveal = false;
				BeginNextTurn(FirstActorFromSeat());
				return;
			}

			// Ahead of the turn clock, and not behind its guard: the hold runs while nobody holds a turn, and
			// a street with no clock at all still has bets to watch.
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

			// No clock at all rather than a hidden one: the bet is the moment the table is meant to
			// take its time over, so a turn with no duration simply waits. Set a duration and the bar
			// appears and the timeout comes back with it.
			if (_turnDuration <= 0f) return;
			if (Data.CurrentTurnClientId.Value == PokerGameData.NoTurn) return;

			_turnElapsed += deltaTime;
			if (_turnElapsed < _turnDuration) return;

			if (AnswerTimedOutTurn(Data.CurrentTurnClientId.Value)) return;

			// Nothing could be answered for them, which is a table set up wrong rather than a rule: say so and
			// give the turn another clock rather than trying again every frame.
			Debug.LogWarning($"[{StageId}] Turn timed out and fold, bet and all in were all refused; check the bettable kinds and the all-in cap in the table's mushroom database.");
			_turnElapsed = 0f;
		}

		// A turn on a clock must always end, whatever the rules have taken away: each answer is tried only when
		// the one before is refused. Folding comes first only where the timeout is set to fold and folding is
		// still allowed (a street, an item or the all-in lock can forbid it); otherwise the table bets a drawn
		// kind for them, and where even that is refused, goes all in.
		private bool AnswerTimedOutTurn(ulong clientId)
		{
			if (_timeoutAction == PokerBetTimeout.Fold && HandleAction(clientId, PokerActionType.Fold, 0)) return true;

			var database = GameMode.BetItemDatabase;
			var fallback = database ? database.DrawBetItemType() : PokerBetItemDatabase.PlainChip;

			if (HandleAction(clientId, PokerActionType.Bet, (int)fallback)) return true;

			return HandleAction(clientId, PokerActionType.AllIn, 0);
		}

		public override bool HandleAction(ulong clientId, PokerActionType action, int amount)
		{
			if (clientId != Data.CurrentTurnClientId.Value) return false;

			var player = GameMode.FindSeatedPlayer(clientId);
			if (!player || !CanBet(player.Data)) return false;

			switch (action)
			{
				case PokerActionType.Bet:
					var itemType = ResolveBetKind(amount);
					if (!IsBettable((int)itemType)) return false;

					// The pot ledger is the only record of who put up what: PokerPotEntry already stamps the
					// owner and the kind, and a second copy on the player would be one the deal's own
					// ServerResetForHand wipes halfway through the round.
					var stakeSize = StakeSize;
					for (var i = 0; i < stakeSize; i++)
					{
						PokerTableUtility.PlaceBet(Data, player, itemType);
					}

					// The reach for the cap. PokerBetItemPotVisual puts the staked cap in this hand on the
					// gesture's own frames, so the two start off the same change.
					player.ActionAnimator?.ServerPlay(PlayerActionIds.Bet);
					break;

				case PokerActionType.AllIn:
					if (!AllowAllIn) return false;

					var database = GameMode.BetItemDatabase;
					if (!database || !database.TryGetEntry(_allInBetItemType, out _))
					{
						Debug.LogWarning($"[{StageId}] All in refused: no entry for {_allInBetItemType} in the table's mushroom database.");
						return false;
					}

					// Going all in still pays this street's bet, so nobody's pile ends shorter than the others';
					// the button asks no kind, so the table draws one, as a bet run out of time does.
					var allInStake = StakeSize;
					for (var i = 0; i < allInStake; i++)
					{
						PokerTableUtility.PlaceBet(Data, player, database.DrawBetItemType());
					}

					// Then the cap itself, staked outright rather than through IsBettable: nobody may choose to
					// bet it, and going all in is the only way it goes up.
					PokerTableUtility.PlaceBet(Data, player, _allInBetItemType);
					player.ActionAnimator?.ServerPlay(PlayerActionIds.Bet);
					_allInCalled = true;
					break;

				case PokerActionType.Fold:
					if (!CanFold(player.Data)) return false;

					player.ServerFold();
					break;

				default:
					return false;
			}

			player.Data.HasActed.Value = true;
			AdvanceTurn(player.Data.SeatIndex.Value);
			return true;
		}

		private PokerBetItemType ResolveBetKind(int requested)
		{
			if (PicksKind) return (PokerBetItemType)requested;

			var database = GameMode.BetItemDatabase;
			return database ? database.DrawBetItemType() : PokerBetItemDatabase.PlainChip;
		}

		// Who this street has a question for. Deliberately not IsInHand: a bet placed **before** the deal
		// finds nobody Active yet, and a hand-based count reads as "everybody has folded" — which once sent
		// the round straight to the reveal, every time, and looped there. Seated and conscious is what
		// is actually being asked; folding is the only thing that takes somebody out of it afterwards.
		// A body that took a free chair mid-match is neither dealt in nor scored, so it is not asked to stake
		// either — the street would sit waiting on an answer from somebody who is only watching.
		public static bool CanBet(PokerPlayerData data) =>
			data && data.IsSeated && data.InMatch.Value && data.IsAlive && data.Status.Value != PokerPlayerStatus.Folded;

		private int CountBettors()
		{
			var count = 0;
			foreach (var player in GameMode.SeatedPlayers)
			{
				if (player && CanBet(player.Data)) count++;
			}

			return count;
		}

		// Asked here so the bar and the server size the offer through the same code: a kind the database
		// does not carry is one the UI can never light and the server will never take.
		public bool IsBettable(int itemType)
		{
			if (itemType <= 0 || itemType > byte.MaxValue) return false;

			var database = GameMode ? GameMode.BetItemDatabase : null;
			if (!database) return false;

			// A kind that exists is not necessarily a kind that may be put up: the Colorful cap is only ever
			// handed to somebody, never staked.
			return database.TryGetEntry((PokerBetItemType)itemType, out var entry) && entry.Bettable;
		}

		public override void HandlePlayerLeft(ulong clientId, int seatIndex)
		{
			if (Data.CurrentTurnClientId.Value != clientId) return;

			AdvanceTurn(seatIndex);
		}

		// The turn is taken off whoever just acted straight away — they have answered, and leaving it on
		// them would let them answer twice — but the next player is not asked until the beat is over, so
		// the bet gesture and the cap landing are watched rather than cut off. Both exits wait: a street
		// that ends on this street has the same animation to finish.
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
			// Going all in closes the betting for everybody, so nobody else is asked on this street.
			if (_allInCalled)
			{
				GameMode.ClearTurn();
				FinishStage(_allInStage);
				return;
			}

			if (CountBettors() <= 1)
			{
				FinishStreet();
				return;
			}

			BeginNextTurn(fromSeatIndex);
		}

		private void BeginNextTurn(int fromSeatIndex)
		{
			var next = PokerTableUtility.NextPlayer(GameMode.SeatedPlayers, fromSeatIndex,
				player => CanBet(player.Data) && !player.Data.HasActed.Value);

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
			var handOver = CountBettors() <= 1;
			FinishStage(handOver ? _handOverStage : null);
		}
	}
}
