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
			Data.Phase.Value = PokerPhase.Waiting;

			GameMode.ClearTurn();
			Data.CommunityCards.Clear();
			Data.RevealedCommunityCards.Value = 0;
			Data.Showdown.Clear();
			Data.Pot.Value = 0;
			Data.CurrentBet.Value = 0;
			Data.LastRaise.Value = 0;

			GameMode.ServerResetMatchStats();

			foreach (var player in GameMode.SeatedPlayers)
			{
				var data = player.Data;
				data.Status.Value = data.Chips > 0 ? PokerPlayerStatus.Waiting : PokerPlayerStatus.Busted;
			}
		}
	}
}
