using Game.Runtime.GameMode.Poker.BetItems;
using Game.Runtime.Player;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Stages
{
	// The opening caps as a beat of their own, before the deal: every player still in the match puts theirs up
	// at once, with the bet gesture. Its own phase, so the pot hands each cap through the player's hand the way
	// a street bet is (BetGrab to BetRelease). The stage lasts the bet clip, so the deal (which puts everybody
	// back to idle) never cuts the gesture off; Exit Delay is any extra wait after it.
	[CreateAssetMenu(fileName = "PokerStage_Ante", menuName = "Game/Poker/Stages/Ante")]
	public class PokerAnteStage : PokerStage
	{
		[Tooltip("Caps each player puts up, each of a kind drawn at random.")]
		[MinValue(1)]
		[SerializeField] private int _anteSize = 1;

		[Tooltip("The bet gesture's clip. The stage waits its length so the caps land before the deal.")]
		[Required]
		[SerializeField] private AnimationClip _betClip;

		private float _timer;

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

			_timer = _betClip ? _betClip.length : 0f;
		}

		protected override void OnTickStage(float deltaTime)
		{
			_timer -= deltaTime;
			if (_timer <= 0f) FinishStage();
		}
	}
}
