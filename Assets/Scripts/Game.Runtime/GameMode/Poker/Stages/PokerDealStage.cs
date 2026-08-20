using System.Collections.Generic;
using Game.Runtime.GameMode.Config;
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

		[Tooltip("How much board this hand needs. Laid on the table face down here, so the streets only turn over what is already lying there.")]
		[SerializeField] private int _communityCardCount = 5;

		[Header("Table")]
		[Tooltip("What a seat costs for the hand, paid by everyone dealt in. Unlike the ante it never lands in front of the player, so the first street still asks them for its price in full — it goes straight into the pot and rides on the hand. Zero seats everyone free.")]
		[MinValue(0)]
		[SerializeField] private int _dealCost = 1;

		[Header("Blinds")]
		[Tooltip("Zero on both leaves the hand unforced — a table where the first street is where money first moves.")]
		[SerializeField] private int _smallBlind = 10;
		[SerializeField] private int _bigBlind = 20;

		[Tooltip("Paid by everyone dealt in, before the blinds — an equal stake in front of every player rather than a bet to answer. Zero plays without one.")]
		[SerializeField] private int _ante;

		[Header("Timing")]
		[Tooltip("Seconds the deal is left on screen. Zero or less moves on the same frame.")]
		[SerializeField] private float _dealDuration = 1.5f;

		private readonly List<CardData> _dealtCards = new();

		public int HoleCardsPerPlayer => Mathf.Max(1, _holeCardsPerPlayer);
		public int CommunityCardCount => Mathf.Max(0, _communityCardCount);
		public int SmallBlind => Mathf.Max(0, _smallBlind);
		public int BigBlind => Mathf.Max(0, _bigBlind);
		public int Ante => Mathf.Max(0, _ante);
		public int DealCost => Mathf.Max(0, _dealCost);

		// The seat cost is exactly what a player has to be able to cover before this stage runs, so it is
		// the same number rather than a second one kept in step.
		public override int UpfrontCostPerPlayer => DealCost;

		protected override void OnCollectConfigEntries(List<MatchConfigEntry> entries)
		{
			entries.Add(new MatchConfigInt(StageId, StageId, "DealCost", "Deal Cost", 0, 10, 1,
				() => _dealCost, value => _dealCost = value));
		}

		protected override void OnStartStage()
		{
			Data.Phase.Value = PokerPhase.Dealing;
			GameMode.ClearTurn();
			Data.CommunityCards.Clear();
			Data.Showdown.Clear();
			Data.Pot.Value = 0;

			RotateDealer();
			DealHoleCards();
			PokerTableUtility.CollectDealCost(Data, GameMode.SeatedPlayers, DealCost);
			PostAnte();
			PostBlinds();

			// A fresh hand straightens everyone back up — whoever spent last hand slumped over a fold
			// comes off that pose here, because nothing else ever tells the gesture layer the hand ended.
			foreach (var player in GameMode.SeatedPlayers)
			{
				player.ActionAnimator?.ServerPlay(PlayerActionIds.Idle);
			}

			if (_dealDuration <= 0f)
			{
				FinishStage();
				return;
			}

			GameMode.BeginStageTimer(_dealDuration);
		}

		protected override void OnTickStage(float deltaTime)
		{
			if (!GameMode.IsStageTimerExpired()) return;

			FinishStage();
		}

		private void RotateDealer()
		{
			var players = GameMode.SeatedPlayers;
			if (players.Count == 0) return;

			var next = PokerTableUtility.NextPlayer(players, Data.DealerSeatIndex.Value, player => player.Data.IsAlive && player.Data.Chips > 0)
			           ?? players[0];

			Data.DealerSeatIndex.Value = next.Data.SeatIndex.Value;
		}

		private void DealHoleCards()
		{
			GameMode.Deck.Rebuild();
			GameMode.Deck.Shuffle();

			// The board comes off the same shuffle up front and goes straight onto the table face down, so
			// the deal owns the whole deck and the streets only turn over what is already lying there.
			var community = new List<CardData>();
			for (var i = 0; i < CommunityCardCount; i++) community.Add(GameMode.Deck.Draw());
			GameMode.ServerDealCommunityCards(community);

			foreach (var player in GameMode.SeatedPlayers)
			{
				var data = player.Data;
				data.ServerResetForHand();

				// Bled out. They keep their chair and watch, but no hand from here on is dealt to them —
				// checked before money, because being dead outranks being broke.
				if (!data.IsAlive)
				{
					data.Status.Value = PokerPlayerStatus.Dead;
					continue;
				}

				if (data.Chips <= 0)
				{
					data.Status.Value = PokerPlayerStatus.Busted;
					continue;
				}

				_dealtCards.Clear();
				for (var card = 0; card < HoleCardsPerPlayer; card++) _dealtCards.Add(GameMode.Deck.Draw());

				data.ServerSetHoleCards(_dealtCards);
				data.Status.Value = PokerPlayerStatus.Active;
			}
		}

		// Straight onto each player's stake rather than through PlaceBet: an ante is not a bet to
		// answer, so it must not raise the table's current bet.
		private void PostAnte()
		{
			if (Ante <= 0) return;

			foreach (var player in GameMode.SeatedPlayers)
			{
				if (player.Data.CanAct) player.Data.ServerPlaceBet(Ante);
			}
		}

		private void PostBlinds()
		{
			if (SmallBlind <= 0 && BigBlind <= 0) return;

			var players = GameMode.SeatedPlayers;

			var smallBlind = PokerTableUtility.NextPlayer(players, Data.DealerSeatIndex.Value, player => player.Data.CanAct);
			if (smallBlind == null) return;

			var bigBlind = PokerTableUtility.NextPlayer(players, smallBlind.Data.SeatIndex.Value, player => player.Data.CanAct);

			PokerTableUtility.PlaceBet(Data, smallBlind, SmallBlind);

			if (bigBlind != null && bigBlind != smallBlind)
			{
				PokerTableUtility.PlaceBet(Data, bigBlind, BigBlind);
			}

			Data.LastRaise.Value = Mathf.Max(BigBlind, Data.CurrentBet.Value);
		}
	}
}
