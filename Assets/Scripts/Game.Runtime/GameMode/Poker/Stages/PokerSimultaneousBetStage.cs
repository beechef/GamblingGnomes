using Game.Runtime.GameMode.Poker.Player;
using Game.Runtime.Player;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Stages
{
	// Everybody bets at once against one shared clock instead of taking turns. Bet is the only thing on
	// offer, so there is no bet to answer, nothing to reopen and no seat order to walk — which is why
	// the turn slot stays empty for the whole stage and the stage clock runs instead.
	[CreateAssetMenu(fileName = "PokerStage_SimultaneousBet", menuName = "Game/Poker/Stages/Simultaneous Bet")]
	public class PokerSimultaneousBetStage : PokerStage
	{
		[Header("Street")]
		[SerializeField] private PokerPhase _phase = PokerPhase.PreFlop;

		[Tooltip("Community cards turned face up as this street opens: 0 pre-flop, 3 on the flop, 1 each on turn and river.")]
		[SerializeField] private int _communityCardsToReveal;

		[Tooltip("On, whatever is already in front of the players stands and this stage bets on top of it — the blinds, for one.")]
		[SerializeField] private bool _keepPreviousBets;

		[Header("Bet")]
		[Tooltip("Smallest bet a player may put in. Equal to the maximum, the amount is fixed and there is nothing to choose.")]
		[SerializeField] private int _minimumBet = 10;

		[Tooltip("Largest bet allowed. Zero or less leaves the ceiling at whatever the player still has.")]
		[SerializeField] private int _maximumBet = 100;

		[Tooltip("Bets snap to this multiple, counted up from the minimum.")]
		[SerializeField] private int _betStep = 10;

		[Tooltip("On, a player may drop the hand instead of betting. Off, betting is the only answer.")]
		[SerializeField] private bool _allowFold;

		[Header("Timing")]
		[Tooltip("Seconds everyone gets to decide. Zero or less runs no clock at all and the street waits for the last bet.")]
		[SerializeField] private float _betDuration = 15f;

		[Tooltip("On, whoever never answered is charged the minimum when the clock runs out. Off, they put nothing in.")]
		[SerializeField] private bool _timeoutBetsMinimum = true;

		[Tooltip("On, whoever never answered folds instead — the street is a call-it-or-leave-it gate. Wins over charging the minimum.")]
		[SerializeField] private bool _timeoutFolds;

		[Header("References")]
		[Tooltip("Where the hand jumps when everyone but one player has folded.")]
		[SerializeField] private PokerStage _handOverStage;

		public int MinimumBet => Mathf.Max(0, _minimumBet);
		public int BetStep => Mathf.Max(1, _betStep);
		public bool AllowFold => _allowFold;

		// The bar and the server both size the bet through these, so what the slider offers is exactly
		// what the server will accept — a short stack simply plays for whatever it has left.
		public int MaximumBetFor(int chips)
		{
			var ceiling = _maximumBet > 0 ? Mathf.Min(_maximumBet, chips) : chips;
			return Mathf.Max(0, ceiling);
		}

		public int MinimumBetFor(int chips) => Mathf.Min(MinimumBet, MaximumBetFor(chips));

		public int ClampBet(int amount, int chips)
		{
			var maximum = MaximumBetFor(chips);
			var minimum = MinimumBetFor(chips);
			var clamped = Mathf.Clamp(amount, minimum, maximum);
			var steps = Mathf.RoundToInt((clamped - minimum) / (float)BetStep);

			return Mathf.Clamp(minimum + steps * BetStep, minimum, maximum);
		}

		protected override void OnStartStage()
		{
			Data.Phase.Value = _phase;

			GameMode.RevealCommunityCards(_communityCardsToReveal);
			GameMode.ClearTurn();

			if (_keepPreviousBets) ClearActionFlags();
			else PokerTableUtility.ResetRoundBets(Data, GameMode.SeatedPlayers);

			if (PokerTableUtility.CountInHand(GameMode.SeatedPlayers) <= 1 || PokerTableUtility.CountActive(GameMode.SeatedPlayers) == 0)
			{
				FinishRound();
				return;
			}

			GameMode.BeginStageTimer(_betDuration);
		}

		protected override void OnEndStage() => GameMode.ClearStageTimer();

		protected override void OnTickStage(float deltaTime)
		{
			if (!GameMode.IsStageTimerExpired()) return;

			ChargeTimeouts();
			FinishRound();
		}

		public override bool HandleAction(ulong clientId, PokerActionType action, int amount)
		{
			if (!IsRunning || IsPaused) return false;

			var player = GameMode.FindSeatedPlayer(clientId);
			if (!player || !player.Data.CanAct) return false;

			// Parallel play has no turn to lose, so this flag is the only thing holding a player to one
			// answer once they have given it.
			if (player.Data.HasActed.Value) return false;

			switch (action)
			{
				case PokerActionType.Bet:
					PlaceBet(player, amount);
					player.ActionAnimator?.ServerPlay(PlayerActionIds.Bet);
					break;

				case PokerActionType.Fold when _allowFold:
					player.ServerFold();
					player.ActionAnimator?.ServerPlay(PlayerActionIds.Fold);
					break;

				default:
					return false;
			}

			player.Data.HasActed.Value = true;

			if (EveryoneLockedIn()) FinishRound();

			return true;
		}

		private void PlaceBet(PokerPlayer player, int amount)
		{
			var bet = ClampBet(amount, player.Data.Chips);
			if (bet <= 0) return;

			// Bets stand side by side rather than against each other, so the table's current bet is left
			// alone: nobody is owed the difference and there is nothing to call.
			player.Data.ServerPlaceBet(bet);
		}

		private void ClearActionFlags()
		{
			foreach (var player in GameMode.SeatedPlayers) player.Data.HasActed.Value = false;
		}

		private void ChargeTimeouts()
		{
			foreach (var player in GameMode.SeatedPlayers)
			{
				var data = player.Data;
				if (!data.CanAct || data.HasActed.Value) continue;

				if (_timeoutFolds)
				{
					player.ServerFold();
					player.ActionAnimator?.ServerPlay(PlayerActionIds.Fold);
				}
				else if (_timeoutBetsMinimum)
				{
					PlaceBet(player, MinimumBet);
					player.ActionAnimator?.ServerPlay(PlayerActionIds.Bet);
				}

				data.HasActed.Value = true;
			}
		}

		private bool EveryoneLockedIn()
		{
			foreach (var player in GameMode.SeatedPlayers)
			{
				if (player.Data.CanAct && !player.Data.HasActed.Value) return false;
			}

			return true;
		}

		private void FinishRound()
		{
			GameMode.ClearStageTimer();
			PokerTableUtility.CollectBets(Data, GameMode.SeatedPlayers);

			var handOver = PokerTableUtility.CountInHand(GameMode.SeatedPlayers) <= 1;
			FinishStage(handOver ? _handOverStage : null);
		}
	}
}
