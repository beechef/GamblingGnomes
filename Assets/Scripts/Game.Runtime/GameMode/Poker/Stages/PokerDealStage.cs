using System.Collections.Generic;
using Game.Runtime.Player;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Stages
{
	[CreateAssetMenu(fileName = "PokerStage_Deal", menuName = "Game/Poker/Stages/Deal")]
	public class PokerDealStage : PokerStage
	{
		[Header("Cards")]
		[SerializeField] private int _holeCardsPerPlayer = 2;

		[Tooltip("How many of their own cards a player may turn over. Zero or less is a hand held the ordinary way, where the holder sees all of it.")]
		[MinValue(0)]
		[SerializeField] private int _viewableHoleCards;

		[Header("Timing")]
		[Tooltip("How long the deal takes to land. The deck's animation reads the same asset, so the stage waits exactly as long as the cards are in the air, plus a rest.")]
		[Required]
		[SerializeField] private PokerDealPacing _pacing;

		private readonly List<CardData> _dealtCards = new();

		public int HoleCardsPerPlayer => Mathf.Max(1, _holeCardsPerPlayer);

		protected override void OnStartStage()
		{
			Data.Phase.Value = PokerPhase.Dealing;
			GameMode.ClearTurn();
			Data.Showdown.Clear();

			var dealtPlayers = DealHoleCards();

			// A fresh hand straightens everyone back up — whoever spent last hand slumped over a fold
			// comes off that pose here, because nothing else ever tells the gesture layer the hand ended.
			foreach (var player in GameMode.SeatedPlayers)
			{
				player.ActionAnimator?.ServerPlay(PlayerActionIds.Idle);
			}

			var duration = _pacing ? _pacing.DealDuration(dealtPlayers, HoleCardsPerPlayer) : 0f;
			if (duration <= 0f)
			{
				FinishStage();
				return;
			}

			GameMode.BeginStageTimer(duration);
		}

		protected override void OnTickStage(float deltaTime)
		{
			if (!GameMode.IsStageTimerExpired()) return;

			FinishStage();
		}

		// Returns how many players were dealt in, which is how long the deal takes to land.
		private int DealHoleCards()
		{
			var dealt = 0;

			GameMode.Deck.Rebuild();
			GameMode.Deck.Shuffle();

			foreach (var player in GameMode.SeatedPlayers)
			{
				var data = player.Data;
				data.ServerResetForHand();

				// Gone under. They keep their chair and watch, but no hand from here on is dealt to them.
				if (!data.IsAlive)
				{
					data.Status.Value = PokerPlayerStatus.Dead;
					continue;
				}

				// Sat down after this match began. They keep the chair and watch it out, and Waiting is
				// already what "seated but never dealt in" means.
				if (!data.InMatch.Value)
				{
					data.Status.Value = PokerPlayerStatus.Waiting;
					continue;
				}

				_dealtCards.Clear();
				for (var card = 0; card < HoleCardsPerPlayer; card++) _dealtCards.Add(GameMode.Deck.Draw());

				// Before the cards, so a hand arrives already knowing how much of itself its holder may see:
				// the view redraws on the list changing, and a limit written after would arrive too late.
				data.ServerSetViewableHoleCards(_viewableHoleCards);
				data.ServerSetHoleCards(_dealtCards);
				data.Status.Value = PokerPlayerStatus.Active;
				dealt++;
			}

			return dealt;
		}
	}
}
