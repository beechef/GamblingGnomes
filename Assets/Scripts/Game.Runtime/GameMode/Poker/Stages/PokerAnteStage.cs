using Game.Runtime.GameMode.Poker.BetItems;
using Game.Runtime.Player;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Stages
{
	// The opening caps as a beat of their own, before the deal: every player still in the match puts theirs up
	// at once, with the bet gesture. Its own phase, so the pot hands each cap through the player's hand the way
	// a street bet is (BetGrab to BetRelease). Exit Delay is how long the table waits before the deal.
	[CreateAssetMenu(fileName = "PokerStage_Ante", menuName = "Game/Poker/Stages/Ante")]
	public class PokerAnteStage : PokerStage
	{
		[Tooltip("Caps each player puts up, each of a kind drawn at random.")]
		[MinValue(1)]
		[SerializeField] private int _anteSize = 1;

		protected override void OnStartStage()
		{
			Data.Phase.Value = PokerPhase.Ante;
			GameMode.ClearTurn();

			var database = GameMode.BetItemDatabase;
			foreach (var player in GameMode.SeatedPlayers)
			{
				if (!player || !GameMode.IsPlayingThisMatch(player.Data)) continue;

				for (var i = 0; i < _anteSize; i++)
				{
					PokerTableUtility.PlaceBet(Data, player, database ? database.DrawBetItemType() : PokerBetItemDatabase.PlainChip);
				}

				player.ActionAnimator?.ServerPlay(PlayerActionIds.Bet);
			}

			FinishStage();
		}
	}
}
