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
		[SerializeField] private bool _allowAllIn = true;

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

		public int MinimumRaiseStep => Mathf.Max(MinimumBet, Data ? Data.LastRaise.Value * MinimumRaiseMultiplier : MinimumBet);

		// A street with a price and no way to move it: the only question left is whether to pay it. The
		// bar asks this rather than working it out from three separate flags, so what the UI shows and
		// what the server accepts can never drift apart.
		public bool IsCallOnly => !AllowRaise && !AllowAllIn && !AllowCheckWhenNoBet;

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

			PostOpeningBet();

			if (PokerTableUtility.CountInHand(GameMode.SeatedPlayers) <= 1)
			{
				FinishStreet();
				return;
			}

			if (PokerTableUtility.CountActive(GameMode.SeatedPlayers) <= 1 && Data.CurrentBet.Value == 0)
			{
				FinishStreet();
				return;
			}

			BeginNextTurn(FirstActorFromSeat());
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

			// Nothing owed makes a call a check by another name, and the announcement should say the one
			// that happened. A short stack calling more than it holds goes in for what it has, which the
			// call itself already clamps to.
			var timeoutAction = _timeoutFolds
				? PokerActionType.Fold
				: owed <= 0
					? PokerActionType.Check
					: PokerActionType.Call;

			// A turn has to end. Every refusal here is a rule saying the polite answer is not available —
			// a street that forbids checking, say — and leaving the turn on the clock would tick this
			// branch forever with the table waiting on somebody who has already stopped answering.
			if (HandleAction(clientId, timeoutAction, 0)) return;

			HandleAction(clientId, PokerActionType.Fold, 0);
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
					if (!_allowAllIn || data.Chips <= 0) return false;

					var previousBet = Data.CurrentBet.Value;
					PokerTableUtility.PlaceBet(Data, player, data.Chips);

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
