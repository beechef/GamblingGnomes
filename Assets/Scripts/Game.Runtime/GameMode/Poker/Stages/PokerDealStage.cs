using System.Collections.Generic;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Stages
{
	public class PokerDealStage : PokerStage
	{
		private readonly List<CardData> _dealtCards = new();

		private float _remaining;

		protected override void OnStartStage()
		{
			Data.Phase.Value = PokerPhase.Dealing;
			GameMode.ClearTurn();
			Data.CommunityCards.Clear();
			Data.Pot.Value = 0;

			RotateDealer();
			DealHoleCards();
			PostBlinds();

			_remaining = Rules.DealDuration;
		}

		protected override void OnTickStage(float deltaTime)
		{
			_remaining -= deltaTime;
			if (_remaining > 0f) return;

			NextStage();
		}

		private void RotateDealer()
		{
			var players = GameMode.SeatedPlayers;
			if (players.Count == 0) return;

			var next = PokerTableUtility.NextPlayer(players, Data.DealerSeatIndex.Value, player => player.Data.Chips.Value > 0)
			           ?? players[0];

			Data.DealerSeatIndex.Value = next.Data.SeatIndex.Value;
		}

		private void DealHoleCards()
		{
			GameMode.Deck.Rebuild();
			GameMode.Deck.Shuffle();

			// The board is burned off the same shuffle up front so the deal stage owns the whole deck
			// and the betting stages only ever flip what is already decided.
			var community = new List<CardData>();
			for (var i = 0; i < Rules.CommunityCardCount; i++) community.Add(GameMode.Deck.Draw());
			GameMode.ServerSetPendingCommunityCards(community);

			foreach (var player in GameMode.SeatedPlayers)
			{
				var data = player.Data;
				data.ServerResetForHand();

				if (data.Chips.Value <= 0)
				{
					data.Status.Value = PokerPlayerStatus.Busted;
					continue;
				}

				_dealtCards.Clear();
				for (var card = 0; card < Rules.HoleCardsPerPlayer; card++) _dealtCards.Add(GameMode.Deck.Draw());

				data.ServerSetHoleCards(_dealtCards);
				data.Status.Value = PokerPlayerStatus.Active;
			}
		}

		private void PostBlinds()
		{
			var players = GameMode.SeatedPlayers;

			var smallBlind = PokerTableUtility.NextPlayer(players, Data.DealerSeatIndex.Value, player => player.Data.CanAct);
			if (smallBlind == null) return;

			var bigBlind = PokerTableUtility.NextPlayer(players, smallBlind.Data.SeatIndex.Value, player => player.Data.CanAct);

			PokerTableUtility.PlaceBet(Data, smallBlind, Rules.SmallBlind);

			if (bigBlind != null && bigBlind != smallBlind)
			{
				PokerTableUtility.PlaceBet(Data, bigBlind, Rules.BigBlind);
			}

			Data.LastRaise.Value = Mathf.Max(Rules.BigBlind, Data.CurrentBet.Value);
		}
	}
}
