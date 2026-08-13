using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Stages
{
	// Turns the board over and holds it there — no bets, no turns, just the table reading what the
	// street revealed. The clock is the whole stage; abilities and reports get their window through
	// the module's stage gates the same way they do anywhere else.
	[CreateAssetMenu(fileName = "PokerStage_Reveal", menuName = "Game/Poker/Stages/Reveal")]
	public class PokerRevealStage : PokerStage
	{
		[Header("Street")]
		[SerializeField] private PokerPhase _phase = PokerPhase.Flop;

		[Tooltip("Community cards turned face up as this stage opens.")]
		[SerializeField] private int _communityCardsToReveal = 5;

		[Header("Timing")]
		[Tooltip("Seconds the table sits with the fresh board before the round moves on.")]
		[SerializeField] private float _revealDuration = 30f;

		[Header("References")]
		[Tooltip("Where the hand jumps when everyone but one player has folded.")]
		[SerializeField] private PokerStage _handOverStage;

		protected override void OnStartStage()
		{
			Data.Phase.Value = _phase;

			GameMode.ClearTurn();
			GameMode.RevealCommunityCards(_communityCardsToReveal);

			if (PokerTableUtility.CountInHand(GameMode.SeatedPlayers) <= 1)
			{
				FinishStage(_handOverStage);
				return;
			}

			if (_revealDuration <= 0f)
			{
				FinishStage();
				return;
			}

			GameMode.BeginStageTimer(_revealDuration);
		}

		protected override void OnEndStage() => GameMode.ClearStageTimer();

		protected override void OnTickStage(float deltaTime)
		{
			if (!GameMode.IsStageTimerExpired()) return;

			FinishStage();
		}
	}
}
