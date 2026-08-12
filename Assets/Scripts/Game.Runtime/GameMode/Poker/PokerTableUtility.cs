using System;
using System.Collections.Generic;
using Game.Runtime.GameMode.Poker.Player;

namespace Game.Runtime.GameMode.Poker
{
	// Pure table mechanics shared by the stages — kept out of the stages themselves so a module
	// writing its own betting stage gets the same seat order and pot rules for free.
	public static class PokerTableUtility
	{
		public static PokerPlayer NextPlayer(IReadOnlyList<PokerPlayer> seatOrder, int fromSeatIndex, Func<PokerPlayer, bool> predicate)
		{
			if (seatOrder.Count == 0) return null;

			var startOffset = 0;
			for (var i = 0; i < seatOrder.Count; i++)
			{
				if (seatOrder[i].Data.SeatIndex.Value <= fromSeatIndex) startOffset = i + 1;
			}

			for (var step = 0; step < seatOrder.Count; step++)
			{
				var player = seatOrder[(startOffset + step) % seatOrder.Count];
				if (predicate(player)) return player;
			}

			return null;
		}

		public static void ResetRoundBets(PokerGameData data, IReadOnlyList<PokerPlayer> players)
		{
			foreach (var player in players) player.Data.ServerResetForRound();

			data.CurrentBet.Value = 0;
			data.LastRaise.Value = 0;
		}

		public static void CollectBets(PokerGameData data, IReadOnlyList<PokerPlayer> players)
		{
			var collected = 0;

			foreach (var player in players)
			{
				collected += player.Data.Bet.Value;
				player.Data.ServerCollectBet();
			}

			if (collected > 0) data.Pot.Value += collected;

			data.CurrentBet.Value = 0;
			data.LastRaise.Value = 0;
		}

		// A player is done when they have had a say and are square with the current bet. All in players
		// have nothing left to say, and folded players are out of the conversation entirely.
		public static bool IsBettingComplete(PokerGameData data, IReadOnlyList<PokerPlayer> players)
		{
			foreach (var player in players)
			{
				if (!player || !player.Data) continue;
				if (!player.Data.CanAct) continue;
				if (!player.Data.HasActed.Value || player.Data.Bet.Value != data.CurrentBet.Value) return false;
			}

			return true;
		}

		public static int PlaceBet(PokerGameData data, PokerPlayer player, int amount)
		{
			var paid = player.Data.ServerPlaceBet(amount);

			if (player.Data.Bet.Value > data.CurrentBet.Value) data.CurrentBet.Value = player.Data.Bet.Value;

			return paid;
		}

		public static void AwardPot(PokerGameData data, IReadOnlyList<PokerPlayer> winners)
		{
			if (winners.Count == 0) return;

			var pot = data.Pot.Value;
			var share = pot / winners.Count;
			var remainder = pot - share * winners.Count;

			for (var i = 0; i < winners.Count; i++)
			{
				winners[i].Data.ServerWinChips(share + (i == 0 ? remainder : 0));
			}

			data.Pot.Value = 0;
		}

		// Null tolerant: a player can be destroyed mid hand by a disconnect, and a count that throws
		// halfway leaves the street unable to decide whether it is over.
		public static int CountInHand(IReadOnlyList<PokerPlayer> players)
		{
			var count = 0;
			foreach (var player in players)
			{
				if (player && player.Data && player.Data.IsInHand) count++;
			}

			return count;
		}

		public static int CountActive(IReadOnlyList<PokerPlayer> players)
		{
			var count = 0;
			foreach (var player in players)
			{
				if (player && player.Data && player.Data.CanAct) count++;
			}

			return count;
		}
	}
}
