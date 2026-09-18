using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Stages
{
	// Idle table. Between two rounds of a match still in progress it deals the next one on its own after
	// a short beat; only once a match is over and everything has been put back does it wait for the host's
	// start button, which is also why this is the only stage players are free to stand up from.
	//
	// Reaching it can also be what makes the next match a new one rather than a continuation: a match
	// plays out over as many hands as it takes, so blood and hallucination go back here when the table
	// says this is where a match ends.
	[CreateAssetMenu(fileName = "PokerStage_Waiting", menuName = "Game/Poker/Stages/Waiting")]
	public class PokerWaitingStage : PokerStage
	{
		[Header("Match")]
		[Tooltip("On, arriving here puts blood, hallucination and everything eaten back to their starting values — this is where a *match* ends. Off, the table only goes idle: a round that comes back here between hands is not a match ending, and resetting there throws away what the players spent the round accumulating.")]
		[SerializeField] private bool _resetMatchStats = true;

		[Header("Round")]
		[Tooltip("Seconds the idle table holds before dealing the next round of a match still in progress. The host's start button is only needed once a match is over and everything has been put back.")]
		[Min(0f)]
		[SerializeField] private float _nextRoundDelay = 1.5f;

		private bool _startingNextRound;
		private float _timer;

		protected override void OnStartStage()
		{
			// The stats go back before the phase says the table is idle, not after. The phase and a player's
			// vitals live on different network objects and arrive in either order, so anything that wakes on
			// "we are waiting now" and then counts what the seats are carrying should be reading numbers that
			// have already been put right.
			if (_resetMatchStats) GameMode.ServerResetMatchStats();

			Data.Phase.Value = PokerPhase.Waiting;

			GameMode.ClearTurn();
			Data.Showdown.Clear();

			// A match that ends at the showdown never reaches the eating, so whatever the settlement handed
			// round would otherwise go on standing in front of everybody through the idle table.
			PokerTableUtility.ResetPot(Data);

			foreach (var player in GameMode.SeatedPlayers)
			{
				var data = player.Data;
				data.Status.Value = GameMode.CanBeDealtIn(data) ? PokerPlayerStatus.Waiting : PokerPlayerStatus.Dead;

				// The same reason as the pot above: the only thing that drops the winner's celebration is the
				// Colorful pick, and a match ending at the showdown goes straight here past it — so the last
				// hand's winner would otherwise go on smiling through the idle table and into the next match.
				player.WinnerPose?.ServerSetSmiling(false);
			}

			// A match in progress is one with enough stamped players still conscious to deal; a finished match has
			// had InMatch put back by the reset, so the idle table after it waits for the host.
			_startingNextRound = GameMode.CanDealAnotherHand;
			_timer = _nextRoundDelay;
		}

		protected override void OnTickStage(float deltaTime)
		{
			if (!_startingNextRound) return;

			_timer -= deltaTime;
			if (_timer > 0f) return;

			_startingNextRound = false;
			GameMode.StartGame();
		}
	}
}
