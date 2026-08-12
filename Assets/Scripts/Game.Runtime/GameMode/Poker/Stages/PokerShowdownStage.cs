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

		[Header("References")]
		[Tooltip("Where the table goes once the hand is settled. Empty simply follows the sequence, which wraps to the idle stage when showdown is last.")]
		[SerializeField] private PokerStage _idleStage;

		private readonly List<PokerPlayer> _winners = new();
		private readonly List<CardData> _evaluationBuffer = new();
		private readonly List<Contender> _ranking = new();

		private readonly struct Contender
		{
			public Contender(PokerPlayer player, PokerHandResult result)
			{
				Player = player;
				Result = result;
			}

			public PokerPlayer Player { get; }
			public PokerHandResult Result { get; }
		}

		protected override void OnStartStage()
		{
			Data.Phase.Value = PokerPhase.Showdown;
			GameMode.ClearTurn();
			PokerTableUtility.CollectBets(Data, GameMode.SeatedPlayers);

			ResolveWinners();

			// Taken before the pot moves so what each player gained can be read off the difference.
			var pot = Data.Pot.Value;
			PokerTableUtility.AwardPot(Data, _winners);

			PublishRanking(pot);

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
			FinishStage(_idleStage);
		}

		private void ResolveWinners()
		{
			_winners.Clear();
			_ranking.Clear();

			var contenders = PokerTableUtility.CountInHand(GameMode.SeatedPlayers);

			foreach (var player in GameMode.SeatedPlayers)
			{
				if (!player.Data.IsInHand) continue;

				// Everyone folding out leaves one player who never has to show what they held.
				if (contenders > 1) player.Data.ServerRevealHand();

				_ranking.Add(new Contender(player, Evaluate(player)));
			}

			// Strongest first, so position in this list is the finishing order.
			_ranking.Sort((left, right) => right.Result.CompareTo(left.Result));

			foreach (var contender in _ranking)
			{
				if (_ranking.Count > 0 && contender.Result.CompareTo(_ranking[0].Result) != 0) break;

				_winners.Add(contender.Player);
			}
		}

		// Ties share a place, and the next player down skips the places they used up — two firsts are
		// followed by a third.
		private void PublishRanking(int pot)
		{
			Data.Showdown.Clear();

			var rank = 0;
			var previous = PokerHandResult.None;

			for (var i = 0; i < _ranking.Count; i++)
			{
				var contender = _ranking[i];
				if (i == 0 || contender.Result.CompareTo(previous) != 0) rank = i + 1;

				previous = contender.Result;

				var share = _winners.Contains(contender.Player) && _winners.Count > 0 ? pot / _winners.Count : 0;

				// The name travels in a fixed buffer, so an overlong one is cut rather than allowed to
				// throw on the way out.
				var handName = contender.Result.DisplayName ?? string.Empty;
				if (handName.Length > 28) handName = handName[..28];

				Data.Showdown.Add(new PokerShowdownEntry
				{
					ClientId = contender.Player.ClientId,
					Rank = rank,
					HandName = handName,
					Winnings = share
				});
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
