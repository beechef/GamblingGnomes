using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Stages
{
	// Cards down, faces up, everybody at once. The wagering is over and there is nothing left to hide, so
	// every hand still in it goes onto the table for the whole room to read — this is the beat the round
	// has been building to, and it is a stage rather than a line inside the showdown because the table
	// needs a moment to look before anybody is told who won.
	//
	// One write does both. HandRevealed makes every slot visible to everyone, and IsHoleCardInHand is
	// derived from it — a revealed hand is by definition not being held — so the cards throw themselves
	// down without a second replicated value saying where they are.
	[CreateAssetMenu(fileName = "PokerStage_CardReveal", menuName = "Game/Poker/Stages/Card Reveal")]
	public class PokerCardRevealStage : PokerStage
	{
		[Header("Timing")]
		[Tooltip("Seconds the table is left to read the hands before the showdown names a winner.")]
		[MinValue(0f)]
		[SerializeField] private float _duration = 3f;

		protected override void OnStartStage()
		{
			GameMode.ClearTurn();

			foreach (var player in GameMode.SeatedPlayers)
			{
				// A folded hand was discarded when it was folded; there is nothing left of it to turn over.
				if (!player || !player.Data || !player.Data.IsInHand) continue;

				player.Data.ServerRevealHand();
			}

			if (_duration <= 0f)
			{
				FinishStage();
				return;
			}

			GameMode.BeginStageTimer(_duration);
		}

		protected override void OnTickStage(float deltaTime)
		{
			if (!GameMode.IsStageTimerExpired()) return;

			FinishStage();
		}
	}
}
