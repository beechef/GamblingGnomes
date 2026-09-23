using System.Collections.Generic;
using Game.Runtime.GameMode.Poker.Hands;
using Game.Runtime.GameMode.Poker.Player;
using Game.Runtime.Player;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Stages
{
	// How a settled hand is paid. Stored as a number in the stage assets, so values are never renumbered.
	public enum PokerSettlement : byte
	{
		// Every loser takes a full copy of what the winner staked; a folder keeps only their fold-phase cap.
		SwapToLosers = 0,

		// Everyone but the winners eats exactly what they staked, folded or beaten alike.
		OwnStake = 1
	}

	[CreateAssetMenu(fileName = "PokerStage_Showdown", menuName = "Game/Poker/Stages/Showdown")]
	public class PokerShowdownStage : PokerStage
	{
		[Header("Hands")]
		[Tooltip("Which hands this showdown recognises and how they rank. Swap the asset to change the ranking wholesale.")]
		[SerializeField] private PokerHandDatabase _handDatabase;

		[Header("Settlement")]
		[Tooltip("Who ends up holding which caps once the hand is decided.")]
		[SerializeField] private PokerSettlement _settlement = PokerSettlement.SwapToLosers;

		[Tooltip("Which street a folder is made to eat their own copy of. The first, by the design — folding after seeing three cards still costs what was put up before them.")]
		[ShowIf(nameof(_settlement), PokerSettlement.SwapToLosers)]
		[SerializeField] private PokerPhase _foldPhase = PokerPhase.FirstStreet;

		[Tooltip("Off, a hand won because everybody else folded stays face down: nobody paid to see it. On, it is turned over and named like any other.")]
		[SerializeField] private bool _revealUncontested = true;

		[Header("Timing")]
		[Tooltip("Seconds the winning hand stays up before the table resets.")]
		[SerializeField] private float _showdownDuration = 5f;

		[Header("References")]
		[Tooltip("Where the table goes when the match still has a hand in it — the deal. Empty ends the match at every showdown, which is one hand per press of start.")]
		[SerializeField] private PokerStage _nextHandStage;

		[Tooltip("Where the table goes once the match is over. Empty simply follows the sequence, which wraps to the idle stage when showdown is last.")]
		[SerializeField] private PokerStage _idleStage;

		[Tooltip("Where the table goes when the match is over, to announce who is left and put everything back. Empty ends the match here and goes straight to the idle stage.")]
		[SerializeField] private PokerStage _matchOverStage;

		private readonly List<CardData> _evaluationBuffer = new();
		private readonly List<Contender> _ranking = new();
		private readonly List<(PokerPlayer Player, int RankGroup)> _contenders = new();
		private readonly List<PokerPlayer> _winners = new();

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

			ResolveContenders();

			// The winner is named before the settlement, because the settlement is about what the winner put
			// up and would otherwise have nobody to ask. Hands that tie all win; the one of them nearest the
			// player who opened the hand is the one named, so a tie is settled the same way every time.
			_winners.Clear();
			foreach (var (player, rankGroup) in _contenders)
			{
				if (rankGroup == 1) _winners.Add(player);
			}

			var winner = NearestToOpener(_winners);
			Data.LastWinnerClientId.Value = winner ? winner.ClientId : PokerGameData.NoTurn;

			// The celebration is held from here until the table comes round to the Colorful pick, so it is
			// raised on the winner and dropped on everybody else — a player who took the last hand and lost
			// this one must not still be wearing it.
			foreach (var player in GameMode.SeatedPlayers)
			{
				if (player) player.WinnerPose?.ServerSetSmiling(player == winner);
			}

			// The winner's cackle, played as the board goes up. Skipped in silence until the art is on the
			// rig, like every gesture.
			if (winner) winner.ActionAnimator?.ServerPlay(PlayerActionIds.Laugh);

			if (_settlement == PokerSettlement.OwnStake) PokerTableUtility.DiscardStakesOf(Data, _winners);
			else PokerTableUtility.SwapPotToLosers(Data, winner, GameMode.SeatedPlayers, GameMode.BetItemDatabase, _foldPhase);

			GameMode.NotifyHandSettled(_winners);

			PublishRanking();

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

		// A match runs itself out: while enough players can still be dealt in, the ranking is the whole
		// ceremony between one hand and the next and nobody has to press anything. Only when the table is
		// down to a single player still in it does the match end — seats unlock, everyone's stats go back,
		// and the host gets the button again.
		private void FinishShowdown()
		{
			// The board comes down with the beat it belongs to. It used to stand until the next deal cleared
			// the list, which in a round that deals halfway through itself meant it sat over the whole
			// settlement — a countdown that ran out and left the board exactly where it was.
			Data.Showdown.Clear();

			if (_nextHandStage && GameMode.CanDealAnotherHand)
			{
				GameMode.EndHand();
				FinishStage(_nextHandStage);
				return;
			}

			if (_matchOverStage)
			{
				FinishStage(_matchOverStage);
				return;
			}

			GameMode.EndGame();
			FinishStage(_idleStage);
		}

		private void ResolveContenders()
		{
			_ranking.Clear();
			_contenders.Clear();

			// Every hand still in is turned over and named — the last one standing after everyone else folded
			// included, unless this table keeps an uncontested hand face down.
			var contested = PokerTableUtility.CountInHand(GameMode.SeatedPlayers) > 1;

			foreach (var player in GameMode.SeatedPlayers)
			{
				if (!player.Data.IsInHand) continue;

				if (!contested && !_revealUncontested)
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

				// The name travels in a fixed buffer, so an overlong one is cut rather than allowed to
				// throw on the way out.
				var handName = result.DisplayName ?? string.Empty;
				if (handName.Length > 28) handName = handName[..28];

				Data.Showdown.Add(new PokerShowdownEntry
				{
					ClientId = player.ClientId,
					Rank = rankGroup,
					HandName = handName
				});
			}
		}

		private PokerHandResult Evaluate(PokerPlayer player)
		{
			_evaluationBuffer.Clear();

			foreach (var card in player.Data.HoleCards) _evaluationBuffer.Add(card);

			// The board counts only as far as it has been turned over: a card nobody has seen is not one
			// anybody's hand was made with.
			for (var i = 0; i < Data.CommunityCards.Count && Data.IsCommunityCardVisible(i); i++)
			{
				_evaluationBuffer.Add(Data.CommunityCards[i]);
			}

			return GameMode.HandEvaluator.Evaluate(_handDatabase, _evaluationBuffer);
		}

		private PokerPlayer NearestToOpener(List<PokerPlayer> candidates)
		{
			if (candidates.Count <= 1) return candidates.Count == 1 ? candidates[0] : null;

			return PokerTableUtility.NextPlayer(GameMode.SeatedPlayers, GameMode.SeatBeforeHandOpener(), candidates.Contains);
		}
	}
}
