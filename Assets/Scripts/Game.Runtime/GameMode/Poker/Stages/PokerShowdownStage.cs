using System.Collections.Generic;
using Game.Runtime.GameMode.Poker.Hands;
using Game.Runtime.GameMode.Poker.Player;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Stages
{
	[CreateAssetMenu(fileName = "PokerStage_Showdown", menuName = "Game/Poker/Stages/Showdown")]
	public class PokerShowdownStage : PokerStage
	{
		[Header("Hands")]
		[Tooltip("Which hands this showdown recognises and how they rank. Swap the asset to change the ranking wholesale.")]
		[SerializeField] private PokerHandDatabase _handDatabase;

		[Header("Timing")]
		[Tooltip("Seconds the winning hand stays up before the table resets.")]
		[SerializeField] private float _showdownDuration = 5f;

		private readonly List<PokerPlayer> _winners = new();
		private readonly List<CardData> _evaluationBuffer = new();

		protected override void OnStartStage()
		{
			Data.Phase.Value = PokerPhase.Showdown;
			GameMode.ClearTurn();
			PokerTableUtility.CollectBets(Data, GameMode.SeatedPlayers);

			ResolveWinners();
			PokerTableUtility.AwardPot(Data, _winners);

			Data.LastWinnerClientId.Value = _winners.Count > 0 ? _winners[0].ClientId : PokerGameData.NoTurn;

			if (_showdownDuration <= 0f)
			{
				FinishShowdown();
				return;
			}

			GameMode.BeginStageTimer(_showdownDuration);
		}

		protected override void OnTickStage(float deltaTime)
		{
			if (!GameMode.IsStageTimerExpired()) return;

			FinishShowdown();
		}

		// Back to the idle table: the hand is over, so seats unlock and the host can deal again.
		private void FinishShowdown()
		{
			GameMode.EndGame();
			GameMode.GoToStage(0);
		}

		private void ResolveWinners()
		{
			_winners.Clear();

			var contenders = PokerTableUtility.CountInHand(GameMode.SeatedPlayers);
			var best = PokerHandResult.None;

			foreach (var player in GameMode.SeatedPlayers)
			{
				if (!player.Data.IsInHand) continue;

				// Everyone folding out leaves one player who never has to show what they held.
				if (contenders > 1) player.Data.ServerRevealHand();

				var result = Evaluate(player);
				var comparison = result.CompareTo(best);

				if (comparison > 0)
				{
					best = result;
					_winners.Clear();
					_winners.Add(player);
				}
				else if (comparison == 0 && _winners.Count > 0)
				{
					_winners.Add(player);
				}
			}
		}

		private PokerHandResult Evaluate(PokerPlayer player)
		{
			_evaluationBuffer.Clear();

			foreach (var card in player.Data.HoleCards) _evaluationBuffer.Add(card);
			foreach (var card in Data.CommunityCards) _evaluationBuffer.Add(card);

			return GameMode.HandEvaluator.Evaluate(_handDatabase, _evaluationBuffer);
		}
	}
}
