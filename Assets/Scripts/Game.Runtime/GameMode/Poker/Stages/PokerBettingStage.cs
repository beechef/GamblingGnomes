using Game.Runtime.GameMode.Poker.Player;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Stages
{
	// One betting street. Which street it is comes from the inspector rather than from subclasses, so
	// a mode with a different board — or a module adding an extra street — is configuration, not code.
	public class PokerBettingStage : PokerStage
	{
		[Header("Street")]
		[SerializeField] private PokerPhase _phase = PokerPhase.PreFlop;

		[Tooltip("Community cards turned face up as this street opens: 0 pre-flop, 3 on the flop, 1 each on turn and river.")]
		[SerializeField] private int _communityCardsToReveal;

		[Tooltip("Pre-flop keeps the blinds standing, every later street starts the betting from scratch.")]
		[SerializeField] private bool _keepPreviousBets;

		[Header("References")]
		[Tooltip("Where the hand jumps when everyone but one player has folded.")]
		[SerializeField] private PokerStage _handOverStage;

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
			if (!player) return;

			var owed = Data.CurrentBet.Value - player.Data.Bet.Value;
			var timeoutAction = owed <= 0 && Rules.TimeoutChecksWhenFree ? PokerActionType.Check : PokerActionType.Fold;

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
					if (owed > 0 || !Rules.AllowCheckWhenNoBet) return false;
					data.HasActed.Value = true;
					break;

				case PokerActionType.Call:
					if (owed <= 0) return false;
					PokerTableUtility.PlaceBet(Data, player, Mathf.Min(owed, data.Chips.Value));
					data.HasActed.Value = true;
					break;

				case PokerActionType.Raise:
				{
					var minimumRaise = Mathf.Max(Rules.BigBlind, Data.LastRaise.Value * Rules.MinimumRaiseMultiplier);
					var target = Mathf.Max(amount, Data.CurrentBet.Value + minimumRaise);
					var toPay = target - data.Bet.Value;
					if (toPay <= owed || data.Chips.Value < toPay) return false;

					var previousBet = Data.CurrentBet.Value;
					PokerTableUtility.PlaceBet(Data, player, toPay);
					Data.LastRaise.Value = Mathf.Max(Rules.BigBlind, Data.CurrentBet.Value - previousBet);

					// A raise reopens the action: everyone still in owes an answer to the new number.
					ReopenAction(player);
					data.HasActed.Value = true;
					break;
				}

				case PokerActionType.AllIn:
				{
					if (!Rules.AllowAllIn || data.Chips.Value <= 0) return false;

					var previousBet = Data.CurrentBet.Value;
					PokerTableUtility.PlaceBet(Data, player, data.Chips.Value);

					if (Data.CurrentBet.Value > previousBet)
					{
						Data.LastRaise.Value = Mathf.Max(Rules.BigBlind, Data.CurrentBet.Value - previousBet);
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

			GameMode.BeginTurn(next.ClientId, Rules.TurnDuration);
		}

		private void FinishStreet()
		{
			GameMode.ClearTurn();
			PokerTableUtility.CollectBets(Data, GameMode.SeatedPlayers);

			if (PokerTableUtility.CountInHand(GameMode.SeatedPlayers) <= 1 && _handOverStage)
			{
				GameMode.GoToStage(_handOverStage);
				return;
			}

			NextStage();
		}
	}
}
