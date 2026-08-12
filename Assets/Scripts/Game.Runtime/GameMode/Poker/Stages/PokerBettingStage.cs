using Game.Runtime.GameMode.Poker.Player;
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
		[SerializeField] private int _minimumBet = 20;

		[SerializeField] private int _minimumRaiseMultiplier = 2;
		[SerializeField] private bool _allowCheckWhenNoBet = true;
		[SerializeField] private bool _allowAllIn = true;

		[Header("Timing")]
		[Tooltip("Seconds each player gets on their turn. Zero or less leaves them unhurried.")]
		[SerializeField] private float _turnDuration = 30f;

		[Tooltip("On, a player who runs out of time checks when nothing is owed and folds otherwise. Off, they always fold.")]
		[SerializeField] private bool _timeoutChecksWhenFree = true;

		[Header("References")]
		[Tooltip("Where the hand jumps when everyone but one player has folded.")]
		[SerializeField] private PokerStage _handOverStage;

		public int MinimumBet => Mathf.Max(0, _minimumBet);
		public int MinimumRaiseMultiplier => Mathf.Max(1, _minimumRaiseMultiplier);
		public bool AllowCheckWhenNoBet => _allowCheckWhenNoBet;
		public bool AllowAllIn => _allowAllIn;

		public int MinimumRaiseStep => Mathf.Max(MinimumBet, Data ? Data.LastRaise.Value * MinimumRaiseMultiplier : MinimumBet);

		protected override void OnStartStage()
		{
			Data.Phase.Value = _phase;

			GameMode.RevealCommunityCards(_communityCardsToReveal);

			if (!_keepPreviousBets) PokerTableUtility.ResetRoundBets(Data, GameMode.SeatedPlayers);

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
			var timeoutAction = owed <= 0 && _timeoutChecksWhenFree ? PokerActionType.Check : PokerActionType.Fold;

			HandleAction(clientId, timeoutAction, 0);
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

			AdvanceAfterAction();
			return true;
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
