using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Stages
{
	// Idle table. Nothing advances it on its own — the host's start button does, which is also why
	// this is the only stage players are free to stand up from.
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
			}
		}
	}
}
