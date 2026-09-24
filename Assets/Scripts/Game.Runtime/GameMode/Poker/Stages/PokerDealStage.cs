using System.Collections.Generic;
using Game.Runtime.GameMode.Poker.BetItems;
using Game.Runtime.Player;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Serialization;

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

		[Tooltip("On, the cards fly from the deck straight into each hand instead of landing on the table first, for a round where nobody chooses which of their cards to look at. Only meaningful with no look limit.")]
		[FormerlySerializedAs("_pickUpWhenDealt")]
		[SerializeField] private bool _dealIntoHand;

		[Tooltip("On, every player's cards face the table and never their holder, until the hand is shown (Indian Poker).")]
		[SerializeField] private bool _hideFromHolder;

		[Tooltip("Cards laid face down in the middle of the table for everyone to share. The streets turn them over; zero plays without a board.")]
		[MinValue(0)]
		[SerializeField] private int _communityCardCount;

		[Header("Stake")]
		[Tooltip("Caps every player dealt in puts up before anybody acts, each of a kind drawn at random. Zero plays without an ante.")]
		[MinValue(0)]
		[SerializeField] private int _anteSize;

		[Header("Timing")]
		[Tooltip("How long the deal takes to land. The deck's animation reads the same asset, so the stage waits exactly as long as the cards are in the air, plus a rest.")]
		[Required]
		[SerializeField] private PokerDealPacing _pacing;

		private readonly List<CardData> _dealtCards = new();

		public int HoleCardsPerPlayer => Mathf.Max(1, _holeCardsPerPlayer);
		public bool DealsIntoHand => _dealIntoHand;
		public int CommunityCardCount => Mathf.Max(0, _communityCardCount);

		protected override void OnStartStage()
		{
			Data.Phase.Value = PokerPhase.Dealing;
			GameMode.ClearTurn();
			Data.Showdown.Clear();

			var dealtPlayers = DealHoleCards();
			DealCommunityCards();
			PostAnte();

			// After the deal, because who opens is asked among the players who were dealt in.
			GameMode.ServerChooseHandOpener();

			// A fresh hand straightens everyone back up — whoever spent last hand slumped over a fold
			// comes off that pose here, because nothing else ever tells the gesture layer the hand ended.
			foreach (var player in GameMode.SeatedPlayers)
			{
				player.ActionAnimator?.ServerPlay(PlayerActionIds.Idle);
			}

			var duration = _pacing ? _pacing.DealDuration(dealtPlayers, HoleCardsPerPlayer, CommunityCardCount, _dealIntoHand) : 0f;
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
				data.ServerSetHiddenFromHolder(_hideFromHolder);

				// Held before they exist, for the same reason: a card arriving already in the hand is built in
				// the fan and flies there from the deck, where one lifted afterwards lands on the table first.
				if (_dealIntoHand) data.ServerPickUpHoleCards(HoleCardsPerPlayer);

				data.ServerSetHoleCards(_dealtCards);
				data.Status.Value = PokerPlayerStatus.Active;
				dealt++;
			}

			return dealt;
		}

		// Off the same shuffle as the hands, after them, and always written — an empty board included — so a
		// table without one never keeps the last hand's.
		private void DealCommunityCards()
		{
			_dealtCards.Clear();
			for (var i = 0; i < CommunityCardCount; i++) _dealtCards.Add(GameMode.Deck.Draw());

			GameMode.ServerDealCommunityCards(_dealtCards);
		}

		private void PostAnte()
		{
			if (_anteSize <= 0) return;

			var database = GameMode.BetItemDatabase;

			foreach (var player in GameMode.SeatedPlayers)
			{
				if (!player || !player.Data.IsInHand) continue;

				for (var i = 0; i < _anteSize; i++)
				{
					PokerTableUtility.PlaceBet(Data, player, database ? database.DrawBetItemType() : PokerBetItemDatabase.PlainChip);
				}
			}
		}
	}
}
