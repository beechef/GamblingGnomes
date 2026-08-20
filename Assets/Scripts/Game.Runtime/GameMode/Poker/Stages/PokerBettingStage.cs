using System.Collections.Generic;
using Game.Runtime.GameMode.Config;
using Game.Runtime.GameMode.Poker.Player;
using Game.Runtime.Player;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Stages
{
	// One betting street, taken in turn. Which street it is and what it costs come from the asset rather
	// than from subclasses, so a mode with a different board — or a module adding an extra street — is
	// another asset in the sequence, not another class.
	[CreateAssetMenu(fileName = "PokerStage_Betting", menuName = "Game/Poker/Stages/Betting")]
	public class PokerBettingStage : PokerStage
	{
		[Header("Street")]
		[SerializeField] private PokerPhase _phase = PokerPhase.PreFlop;

		[Tooltip("Community cards turned face up as this street opens: 0 pre-flop, 3 on the flop, 1 each on turn and river.")]
		[SerializeField] private int _communityCardsToReveal;

		[Tooltip("On, whatever is already in front of the players stands — the blinds, pre-flop. Off, the street starts from scratch.")]
		[SerializeField] private bool _keepPreviousBets;

		[Header("Bet")]
		[Tooltip("Smallest a raise may move the bet. The blind, on a normal table.")]
		[MinValue(0)]
		[SerializeField] private int _minimumBet = 20;

		[Tooltip("What the street costs to stay in, owed by everyone the moment it opens. Zero opens it free, so the price is whatever somebody bets — set it, turn raising off and there is nothing left to say but call or fold.")]
		[MinValue(0)]
		[SerializeField] private int _openingBet;

		[MinValue(1)]
		[SerializeField] private int _minimumRaiseMultiplier = 2;
		[SerializeField] private bool _allowCheckWhenNoBet = true;
		[SerializeField] private bool _allowRaise = true;

		[Tooltip("On, shoving is always on offer and costs up to the maximum below. Off, the petal only lights for a player who cannot cover the price already standing — which is not a shove at all, only a call they are short of.")]
		[SerializeField] private bool _allowAllIn = true;

		[Tooltip("Most a shove may put in at once. A player short of the price already standing still pays everything they hold, because that is a call they cannot cover rather than a shove, and capping it would leave them owing on a hand they meant to answer in full.")]
		[MinValue(1)]
		[SerializeField] private int _maximumAllIn = 4;

		[Header("Timing")]
		[Tooltip("Seconds each player gets on their turn. Zero or less leaves them unhurried.")]
		[SerializeField] private float _turnDuration = 30f;

		[Tooltip("On, running out of time throws the hand away. Off — the default — it pays what is owed, or checks when nothing is: a player who says nothing is usually reading the table, not leaving it, and folding a hand they never chose to fold is the harshest reading of a silence.")]
		[SerializeField] private bool _timeoutFolds;

		[Header("References")]
		[Tooltip("Where the hand jumps when everyone but one player has folded.")]
		[SerializeField] private PokerStage _handOverStage;

		public int MinimumBet => Mathf.Max(0, _minimumBet);
		public int OpeningBet => Mathf.Max(0, _openingBet);
		public int MinimumRaiseMultiplier => Mathf.Max(1, _minimumRaiseMultiplier);
		public bool AllowCheckWhenNoBet => _allowCheckWhenNoBet;
		public bool AllowRaise => _allowRaise;
		public bool AllowAllIn => _allowAllIn;
		public int MaximumAllIn => Mathf.Max(1, _maximumAllIn);

		public int MinimumRaiseStep => Mathf.Max(MinimumBet, Data ? Data.LastRaise.Value * MinimumRaiseMultiplier : MinimumBet);

		// A street with a price and no way to move it: the only question left is whether to pay it. The
		// bar asks this rather than working it out from separate flags, so what the UI shows and what the
		// server accepts can never drift apart.
		//
		// All-in is deliberately not part of it. Raising and checking change the *question* the street is
		// asking; a shove only changes how much of an answer is available — so a table that turns shoving
		// on has not stopped being a call-or-fold street, and the pad must not hand its verbs back to the
		// full bar over it.
		public bool IsCallOnly => !AllowRaise && !AllowCheckWhenNoBet;

		// What a shove costs this player right now. Everything, when the price already standing is more
		// than they hold — that is a call they are short of, and it has to be paid in full or they are
		// left owing. The capped amount otherwise, which is the shove the table actually offers.
		public int AllInAmountFor(PokerPlayerData data)
		{
			if (!data || !Data) return 0;

			var owed = Data.CurrentBet.Value - data.Bet.Value;

			return owed >= data.Chips ? data.Chips : Mathf.Min(data.Chips, MaximumAllIn);
		}

		// Whether calling is still an answer of its own. A call that would cost this player everything is
		// not a call — it is the shove standing next to it, and offering both would be the same move under
		// two words, one of which announces the wrong thing to the table.
		//
		// Asked by the pad rather than by the server: the server keeps taking Call from a short stack and
		// paying what it can, which is what an expired turn falls back on. Refusing to *offer* something the
		// server would still accept is the safe direction; the other way round is the one that strands a
		// lit button on a move that gets dropped.
		public bool CanCall(PokerPlayerData data)
		{
			if (!data || !Data || data.Chips <= 0) return false;

			var owed = Data.CurrentBet.Value - data.Bet.Value;

			return owed > 0 && owed < data.Chips;
		}

		// Whether the petal is on offer at all: always, where the table allows shoving, and otherwise only
		// to somebody who cannot cover what is owed. Read by the pad and by the server from one place.
		public bool CanAllIn(PokerPlayerData data)
		{
			if (!data || !Data || data.Chips <= 0) return false;

			return AllowAllIn || Data.CurrentBet.Value - data.Bet.Value >= data.Chips;
		}

		protected override void OnCollectConfigEntries(List<MatchConfigEntry> entries)
		{
			entries.Add(new MatchConfigInt(StageId, StageId, "OpeningBet", "Opening Bet", 0, 20, 1,
				() => _openingBet, value => _openingBet = value));
			entries.Add(new MatchConfigBool(StageId, StageId, "AllowAllIn", "Allow All-In",
				() => _allowAllIn, value => _allowAllIn = value));
			entries.Add(new MatchConfigInt(StageId, StageId, "MaximumAllIn", "Max All-In", 1, 99, 1,
				() => _maximumAllIn, value => _maximumAllIn = value));
			entries.Add(new MatchConfigFloat(StageId, StageId, "TurnDuration", "Turn Seconds", 5f, 120f, 5f,
				() => _turnDuration, value => _turnDuration = value, "0"));
		}

		private const float MinimumResumedTurnSeconds = 1f;

		private bool _hasPausedTurn;
		private ulong _pausedTurnClientId;
		private int _pausedTurnSeatIndex;
		private float _pausedTurnRemaining;

		protected override void OnStartStage()
		{
			_hasPausedTurn = false;

			Data.Phase.Value = _phase;

			GameMode.RevealCommunityCards(_communityCardsToReveal);

			if (!_keepPreviousBets) PokerTableUtility.ResetRoundBets(Data, GameMode.SeatedPlayers);

			if (PokerTableUtility.CountInHand(GameMode.SeatedPlayers) <= 1)
			{
				FinishStreet();
				return;
			}

			// Asked before the street posts its price, not after. Once the table is all-in but for one
			// player there is nobody left to bet against, and a street that had already put its opening bet
			// down would go on to charge the last player standing for the privilege — money nobody can
			// answer, on a hand whose betting is over. The cards still turn; only the asking stops.
			if (NothingLeftToAsk())
			{
				FinishStreet();
				return;
			}

			PostOpeningBet();

			BeginNextTurn(FirstActorFromSeat());
		}

		// Whether this street has a question for anybody. Two players who can still act have something to
		// settle between them; one has nobody to settle with, and is only held here if they still owe the
		// table — an all-in raise from the last street they have not answered yet.
		private bool NothingLeftToAsk()
		{
			var players = GameMode.SeatedPlayers;
			if (PokerTableUtility.CountActive(players) > 1) return false;

			foreach (var player in players)
			{
				if (!player.Data.CanAct) continue;
				if (Data.CurrentBet.Value - player.Data.Bet.Value > 0) return false;
			}

			return true;
		}

		// The price of staying in, put on the table by the street itself rather than by a player. Nobody
		// has paid it yet, so everyone still in owes it — which is what leaves call and fold as the only
		// answers. It never undercuts a price the table already stands at, so it is safe on a street that
		// keeps the blinds in front of the players.
		private void PostOpeningBet()
		{
			if (OpeningBet <= 0 || Data.CurrentBet.Value >= OpeningBet) return;

			Data.CurrentBet.Value = OpeningBet;
			Data.LastRaise.Value = Mathf.Max(Data.LastRaise.Value, OpeningBet);
		}

		// Pre-flop the blinds are already out, and the big blind is owed the last word — so the action
		// opens after them rather than after the dealer the way every later street does.
		private int FirstActorFromSeat()
		{
			if (!_keepPreviousBets) return Data.DealerSeatIndex.Value;

			var lastBlindSeat = Data.DealerSeatIndex.Value;

			foreach (var player in GameMode.SeatedPlayers)
			{
				if (player.Data.Bet.Value > 0) lastBlindSeat = player.Data.SeatIndex.Value;
			}

			return lastBlindSeat;
		}

		protected override void OnEndStage()
		{
			GameMode.ClearTurn();
		}

		// The turn clock is an absolute server time and would drain away under an overlay. The whole
		// turn comes down with it, not just the clock — whatever runs on top gets the turn slot to
		// itself, and the banner stops counting a player down while they cannot act.
		protected override void OnPauseStage()
		{
			_hasPausedTurn = Data.HasTurn;
			if (!_hasPausedTurn) return;

			_pausedTurnClientId = Data.CurrentTurnClientId.Value;
			_pausedTurnRemaining = Data.TurnRemaining;

			var player = GameMode.FindSeatedPlayer(_pausedTurnClientId);
			_pausedTurnSeatIndex = player ? player.Data.SeatIndex.Value : Data.DealerSeatIndex.Value;

			GameMode.ClearTurn();
		}

		// An overlay can change the table under the pause — fold somebody in a lost callout, lose a
		// player to a disconnect that only the overlay heard about — so the street re-reads the table
		// rather than trusting what it left behind.
		protected override void OnResumeStage()
		{
			if (!_hasPausedTurn) return;

			_hasPausedTurn = false;

			var players = GameMode.SeatedPlayers;

			if (PokerTableUtility.CountInHand(players) <= 1 || PokerTableUtility.IsBettingComplete(Data, players))
			{
				FinishStreet();
				return;
			}

			var player = GameMode.FindSeatedPlayer(_pausedTurnClientId);
			if (player && player.Data.CanAct)
			{
				var duration = _turnDuration <= 0f ? 0f : Mathf.Max(_pausedTurnRemaining, MinimumResumedTurnSeconds);
				GameMode.BeginTurn(_pausedTurnClientId, duration);
				return;
			}

			// Whoever was on the clock is gone — continue from their seat so the order stays as it was.
			BeginNextTurn(_pausedTurnSeatIndex);
		}

		protected override void OnTickStage(float deltaTime)
		{
			if (!Data.HasTurn) return;
			if (!GameMode.IsTurnExpired()) return;

			var clientId = Data.CurrentTurnClientId.Value;
			var player = GameMode.FindSeatedPlayer(clientId);

			// The turn points at somebody who is no longer at the table — a disconnect that raced the
			// callback, say. Treat it as them leaving rather than sitting here waiting on a ghost.
			if (!player)
			{
				HandlePlayerLeft(clientId, -1);
				return;
			}

			var owed = Data.CurrentBet.Value - player.Data.Bet.Value;

			// A turn has to end, and leaving it on the clock would tick this branch forever with the table
			// waiting on somebody who has already stopped answering. Fold is the one answer nothing can
			// refuse — and also the harshest thing that can happen to a player who merely went quiet — so
			// it is reached for last, after every answer that costs them nothing has been tried in turn.
			//
			// Nothing owed makes a call a check by another name, and the announcement should say the one
			// that happened. A short stack calling more than it holds goes in for what it has, which the
			// call itself already clamps to.
			if (!_timeoutFolds)
			{
				var data = player.Data;

				// Call first, and only where it is really a call — asked through the same method the pad
				// lights that petal from, so a turn that runs out makes the move the player was being
				// offered rather than a differently named one.
				if (CanCall(data) && HandleAction(clientId, PokerActionType.Call, 0)) return;

				// Short of the price: in for what they hold. Only ever the call they cannot cover, never a
				// shove of their own — a silence is not a decision to put everything on the table.
				if (owed > 0 && owed >= data.Chips && HandleAction(clientId, PokerActionType.AllIn, 0)) return;

				if (HandleAction(clientId, PokerActionType.Check, 0)) return;
				if (owed <= 0 && PassSquareTurn(player)) return;
			}

			HandleAction(clientId, PokerActionType.Fold, 0);
		}

		// Owing nothing, this player is already square with the street and it has nothing left to ask them.
		// A street may still forbid checking, but that rule is about choosing to tap the table instead of
		// putting something in — applying it to a silence throws a paid-up hand away for a rule nobody
		// wrote, which is how a call-or-fold street ended up folding everyone who stopped clicking.
		private bool PassSquareTurn(PokerPlayer player)
		{
			player.Data.HasActed.Value = true;

			AdvanceAfterAction();
			return true;
		}

		public override bool HandleAction(ulong clientId, PokerActionType action, int amount)
		{
			if (!IsRunning || IsPaused) return false;
			if (Data.CurrentTurnClientId.Value != clientId) return false;

			var player = GameMode.FindSeatedPlayer(clientId);
			if (!player || !player.Data.CanAct) return false;

			var data = player.Data;
			var owed = Data.CurrentBet.Value - data.Bet.Value;

			switch (action)
			{
				case PokerActionType.Fold:
					data.Status.Value = PokerPlayerStatus.Folded;
					data.HasActed.Value = true;
					break;

				case PokerActionType.Check:
					if (owed > 0 || !_allowCheckWhenNoBet) return false;
					data.HasActed.Value = true;
					break;

				case PokerActionType.Call:
					if (owed <= 0) return false;
					PokerTableUtility.PlaceBet(Data, player, Mathf.Min(owed, data.Chips));
					data.HasActed.Value = true;
					break;

				case PokerActionType.Raise:
				{
					if (!_allowRaise) return false;

					var target = Mathf.Max(amount, Data.CurrentBet.Value + MinimumRaiseStep);
					var toPay = target - data.Bet.Value;
					if (toPay <= owed || data.Chips < toPay) return false;

					var previousBet = Data.CurrentBet.Value;
					PokerTableUtility.PlaceBet(Data, player, toPay);
					Data.LastRaise.Value = Mathf.Max(MinimumBet, Data.CurrentBet.Value - previousBet);

					// A raise reopens the action: everyone still in owes an answer to the new number.
					ReopenAction(player);
					data.HasActed.Value = true;
					break;
				}

				case PokerActionType.AllIn:
				{
					// A street can forbid shoving without being able to forbid a short stack paying what it
					// has. The permission is about *choosing* to put everything in and move the price; a
					// player who cannot cover the price already standing is not moving anything, and
					// refusing them here would leave folding as their only answer to a call they can make.
					// Asked through the same method the pad lights its petal from, so the two cannot drift.
					if (!CanAllIn(data)) return false;

					var previousBet = Data.CurrentBet.Value;
					PokerTableUtility.PlaceBet(Data, player, AllInAmountFor(data));

					if (Data.CurrentBet.Value > previousBet)
					{
						Data.LastRaise.Value = Mathf.Max(MinimumBet, Data.CurrentBet.Value - previousBet);
						ReopenAction(player);
					}

					data.HasActed.Value = true;
					break;
				}

				default:
					return false;
			}

			PlayActionGesture(player, action);

			AdvanceAfterAction();
			return true;
		}

		// The act, seen: the fold thrown, the coin put up. Checks stay quiet — the list of gestures is the
		// database's business, this only names which act just happened.
		private static void PlayActionGesture(PokerPlayer player, PokerActionType action)
		{
			var animator = player.ActionAnimator;
			if (!animator) return;

			switch (action)
			{
				case PokerActionType.Fold:
					animator.ServerPlay(PlayerActionIds.Fold);
					break;

				case PokerActionType.Call:
				case PokerActionType.Raise:
				case PokerActionType.AllIn:
				case PokerActionType.Bet:
					animator.ServerPlay(PlayerActionIds.Bet);
					break;
			}
		}

		private void ReopenAction(PokerPlayer raiser)
		{
			foreach (var other in GameMode.SeatedPlayers)
			{
				if (other == raiser || !other.Data.CanAct) continue;

				other.Data.HasActed.Value = false;
			}
		}

		// Somebody left. If the street was waiting on them it has to move itself along, and if their
		// leaving ended the hand it has to say so — nothing else will arrive to do it.
		public override void HandlePlayerLeft(ulong clientId, int seatIndex)
		{
			if (!IsRunning || IsPaused) return;

			var wasTheirTurn = Data.CurrentTurnClientId.Value == clientId;
			if (wasTheirTurn) GameMode.ClearTurn();

			var players = GameMode.SeatedPlayers;

			if (PokerTableUtility.CountInHand(players) <= 1 || PokerTableUtility.IsBettingComplete(Data, players))
			{
				FinishStreet();
				return;
			}

			if (!wasTheirTurn) return;

			// Continue from the seat they vacated so the order stays as it was.
			BeginNextTurn(seatIndex >= 0 ? seatIndex : Data.DealerSeatIndex.Value);
		}

		private void AdvanceAfterAction()
		{
			var players = GameMode.SeatedPlayers;

			if (PokerTableUtility.CountInHand(players) <= 1 || PokerTableUtility.IsBettingComplete(Data, players))
			{
				FinishStreet();
				return;
			}

			var current = GameMode.FindSeatedPlayer(Data.CurrentTurnClientId.Value);
			BeginNextTurn(current ? current.Data.SeatIndex.Value : Data.DealerSeatIndex.Value);
		}

		private void BeginNextTurn(int fromSeatIndex)
		{
			var next = PokerTableUtility.NextPlayer(GameMode.SeatedPlayers, fromSeatIndex,
				player => player.Data.CanAct && (!player.Data.HasActed.Value || player.Data.Bet.Value != Data.CurrentBet.Value));

			if (next == null)
			{
				FinishStreet();
				return;
			}

			GameMode.BeginTurn(next.ClientId, _turnDuration);
		}

		private void FinishStreet()
		{
			GameMode.ClearTurn();
			PokerTableUtility.CollectBets(Data, GameMode.SeatedPlayers);

			var handOver = PokerTableUtility.CountInHand(GameMode.SeatedPlayers) <= 1;
			FinishStage(handOver ? _handOverStage : null);
		}
	}
}
