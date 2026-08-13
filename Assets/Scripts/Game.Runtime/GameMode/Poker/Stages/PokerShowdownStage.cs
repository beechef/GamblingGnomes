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

		private readonly List<CardData> _evaluationBuffer = new();
		private readonly List<Contender> _ranking = new();
		private readonly List<(PokerPlayer Player, int RankGroup)> _contenders = new();
		private readonly Dictionary<ulong, int> _winnings = new();

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

			ResolveContenders();
			PokerTableUtility.SettlePots(Data, GameMode.SeatedPlayers, _contenders, _winnings);
			PublishRanking();

			Data.LastWinnerClientId.Value = _contenders.Count > 0 ? _contenders[0].Player.ClientId : PokerGameData.NoTurn;

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

		private void ResolveContenders()
		{
			_ranking.Clear();
			_contenders.Clear();

			var inHand = PokerTableUtility.CountInHand(GameMode.SeatedPlayers);

			foreach (var player in GameMode.SeatedPlayers)
			{
				if (!player.Data.IsInHand) continue;

				// Everyone folding out leaves one player who never has to show what they held — or say
				// what it was worth, so the hand is not even evaluated on their behalf.
				if (inHand <= 1)
				{
					_ranking.Add(new Contender(player, PokerHandResult.None));
					continue;
				}

				player.Data.ServerRevealHand();
				_ranking.Add(new Contender(player, Evaluate(player)));
			}

			// Strongest first, so position in this list is the finishing order. Ties share a rank group,
			// which is also how the settlement knows two hands are worth the same.
			_ranking.Sort((left, right) => right.Result.CompareTo(left.Result));

			var rankGroup = 0;
			var previous = PokerHandResult.None;

			for (var i = 0; i < _ranking.Count; i++)
			{
				if (i == 0 || _ranking[i].Result.CompareTo(previous) != 0) rankGroup = i + 1;

				previous = _ranking[i].Result;
				_contenders.Add((_ranking[i].Player, rankGroup));
			}
		}

		// Ties share a place, and the next player down skips the places they used up — two firsts are
		// followed by a third.
		private void PublishRanking()
		{
			Data.Showdown.Clear();

			for (var i = 0; i < _contenders.Count; i++)
			{
				var (player, rankGroup) = _contenders[i];
				var result = _ranking[i].Result;

				_winnings.TryGetValue(player.ClientId, out var won);

				// The name travels in a fixed buffer, so an overlong one is cut rather than allowed to
				// throw on the way out.
				var handName = result.DisplayName ?? string.Empty;
				if (handName.Length > 28) handName = handName[..28];

				Data.Showdown.Add(new PokerShowdownEntry
				{
					ClientId = player.ClientId,
					Rank = rankGroup,
					HandName = handName,
					Winnings = won
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
