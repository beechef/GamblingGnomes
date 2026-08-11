using System.Collections.Generic;
using Game.Runtime.GameMode.Poker.Hands;
using Game.Runtime.GameMode.Poker.Player;

namespace Game.Runtime.GameMode.Poker.Stages
{
	public class PokerShowdownStage : PokerStage
	{
		private readonly List<PokerPlayer> _winners = new();
		private readonly List<CardData> _evaluationBuffer = new();

		private float _remaining;

		protected override void OnStartStage()
		{
			Data.Phase.Value = PokerPhase.Showdown;
			GameMode.ClearTurn();
			PokerTableUtility.CollectBets(Data, GameMode.SeatedPlayers);

			ResolveWinners();
			PokerTableUtility.AwardPot(Data, _winners);

			Data.LastWinnerClientId.Value = _winners.Count > 0 ? _winners[0].ClientId : PokerGameData.NoTurn;

			_remaining = Rules.ShowdownDuration;
		}

		protected override void OnTickStage(float deltaTime)
		{
			_remaining -= deltaTime;
			if (_remaining > 0f) return;

			// Back to the idle table: the hand is over, so seats unlock and the host can deal again.
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

			return GameMode.HandEvaluator.Evaluate(Rules.HandDatabase, _evaluationBuffer);
		}
	}
}
