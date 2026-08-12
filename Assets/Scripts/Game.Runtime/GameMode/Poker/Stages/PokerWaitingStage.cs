using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Stages
{
	// Idle table. Nothing advances it on its own — the host's start button does, which is also why
	// this is the only stage players are free to stand up from.
	[CreateAssetMenu(fileName = "PokerStage_Waiting", menuName = "Game/Poker/Stages/Waiting")]
	public class PokerWaitingStage : PokerStage
	{
		protected override void OnStartStage()
		{
			Data.Phase.Value = PokerPhase.Waiting;

			GameMode.ClearTurn();
			Data.CommunityCards.Clear();
			Data.Pot.Value = 0;
			Data.CurrentBet.Value = 0;
			Data.LastRaise.Value = 0;

			foreach (var player in GameMode.SeatedPlayers)
			{
				var data = player.Data;
				data.ServerResetForHand();
				data.Status.Value = data.Chips.Value > 0 ? PokerPlayerStatus.Waiting : PokerPlayerStatus.Busted;
			}
		}
	}
}
