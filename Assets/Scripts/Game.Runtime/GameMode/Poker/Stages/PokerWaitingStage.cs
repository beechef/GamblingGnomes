using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Stages
{
	// Idle table. Nothing advances it on its own — the host's start button does, which is also why
	// this is the only stage players are free to stand up from.
	//
	// Reaching it is also what makes the next match a new one rather than a continuation: a match plays
	// out over as many hands as it takes, so blood and money go back here, at the one point that is only
	// ever arrived at with the previous match already over.
	[CreateAssetMenu(fileName = "PokerStage_Waiting", menuName = "Game/Poker/Stages/Waiting")]
	public class PokerWaitingStage : PokerStage
	{
		protected override void OnStartStage()
		{
			// Blood and money go back before the phase says the table is idle, not after. The phase and a
			// player's purse live on different network objects and arrive in either order, so anything that
			// wakes on "we are waiting now" and then counts what the seats are carrying should be reading
			// numbers that have already been put right.
			GameMode.ServerResetMatchStats();

			Data.Phase.Value = PokerPhase.Waiting;

			GameMode.ClearTurn();
			Data.CommunityCards.Clear();
			Data.RevealedCommunityCards.Value = 0;
			Data.Showdown.Clear();
			Data.Pot.Value = 0;
			Data.CurrentBet.Value = 0;
			Data.LastRaise.Value = 0;

			foreach (var player in GameMode.SeatedPlayers)
			{
				var data = player.Data;
				data.Status.Value = data.Chips > 0 ? PokerPlayerStatus.Waiting : PokerPlayerStatus.Busted;
			}
		}
	}
}
