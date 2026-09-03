using System;
using System.Collections.Generic;
using Game.Runtime.GameMode.Poker.Mushrooms;
using Game.Runtime.GameMode.Poker.Player;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker
{
	// Pure table mechanics shared by the stages — kept out of the stages themselves so a module
	// writing its own betting stage gets the same seat order and pot rules for free.
	public static class PokerTableUtility
	{
		// Scratch space for the settlement, not state — cleared before every use, reset anyway because
		// statics survive between play sessions with Domain Reload off.
		private static readonly List<int> CapBuffer = new();
		private static readonly List<PokerPlayer> WinnerBuffer = new();
		private static readonly List<byte> TypeBuffer = new();

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void ResetStatics()
		{
			CapBuffer.Clear();
			WinnerBuffer.Clear();
			TypeBuffer.Clear();
		}

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

		// What it costs to be dealt in, as opposed to what is bet on the hand: it goes straight to the pot
		// without ever sitting in front of a player, so the first street still asks them for its price in
		// full. SettlePots hands it to whoever wins, the same as any bet nobody could cover.
		public static int CollectDealCost(PokerGameData data, IReadOnlyList<PokerPlayer> players, int amount)
		{
			if (amount <= 0) return 0;

			var collected = 0;

			foreach (var player in players)
			{
				if (!player || !player.Data || !player.Data.CanAct) continue;

				TypeBuffer.Clear();
				var paid = player.Data.ServerPayIntoPot(amount, TypeBuffer);
				if (paid > 0) AddPotItems(data, player.ClientId, TypeBuffer);
				collected += paid;
			}

			if (collected > 0) data.Pot.Value += collected;

			return collected;
		}

		public static void CollectBets(PokerGameData data, IReadOnlyList<PokerPlayer> players)
		{
			var collected = 0;

			foreach (var player in players)
			{
				var bet = player.Data.Bet.Value;
				if (bet > 0)
				{
					player.Data.ServerTakeCommittedItemTypes(bet, TypeBuffer);
					AddPotItems(data, player.ClientId, TypeBuffer);
				}

				collected += bet;
				player.Data.ServerCollectBet();
			}

			if (collected > 0) data.Pot.Value += collected;

			data.CurrentBet.Value = 0;
			data.LastRaise.Value = 0;
		}

		// Dead money from a seat abandoned mid-hand: swept into the pot now, because once the player's
		// object despawns no street-end sweep will ever see it. The one write to the pot that does not
		// happen at a street's end, which is exactly why it lives here beside the others.
		public static void ForfeitBet(PokerGameData data, PokerPlayer player)
		{
			var bet = player.Data.Bet.Value;
			if (bet <= 0) return;

			player.Data.ServerTakeCommittedItemTypes(bet, TypeBuffer);
			AddPotItems(data, player.ClientId, TypeBuffer);
			data.Pot.Value += bet;
			player.Data.ServerCollectBet();
		}

		public static void ResetPot(PokerGameData data)
		{
			data.Pot.Value = 0;
			data.PotItems.Clear();
		}

		// The pot's itemised half. One entry per unit staked, stamped with who fed it, on which street,
		// and what it is — the types were drawn off the front of the owner's wallet when the stake was
		// placed. Only ever written beside the scalar, in this class, so the two cannot disagree.
		private static void AddPotItems(PokerGameData data, ulong ownerClientId, List<byte> itemTypes)
		{
			foreach (var itemType in itemTypes)
			{
				data.PotItems.Add(new PokerBetItem
				{
					OwnerClientId = ownerClientId,
					Phase = data.Phase.Value,
					ItemTypeIndex = itemType
				});
			}
		}

		// The loser-eats settlement: the pot is not won, it is swallowed. Every unit goes down the
		// eaters' throats in pot order — ties share the plate unit by unit — and each one does whatever
		// its kind does. Nothing is ever paid out: winning here is worth exactly not having to eat,
		// the same shape as the report's "an accusation costs the loser; it never pays the winner".
		public static void FeedPot(PokerGameData data, IReadOnlyList<PokerPlayer> eaters,
			PokerMushroomDatabase database, PokerGameMode gameMode)
		{
			var pot = data.Pot.Value;
			data.Pot.Value = 0;

			if (pot > 0 && eaters.Count > 0)
			{
				for (var i = 0; i < data.PotItems.Count; i++)
				{
					var eater = eaters[i % eaters.Count];
					if (!eater || !eater.Data) continue;

					if (database && database.TryGetEntry(data.PotItems[i].ItemTypeIndex, out var entry) && entry.Effect)
					{
						entry.Effect.ConsumeServer(gameMode, eater);
					}
				}
			}

			data.PotItems.Clear();
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

		// Settles the pot against what each player actually put in, so a short stack that went all in
		// only wins the slice of the pot it matched — the rest cascades down to the next hand that
		// covered it, and an uncalled bet falls straight back to its owner. Contenders come strongest
		// first; an equal RankGroup means an equal hand, and equals split their slice.
		public static void SettlePots(PokerGameData data, IReadOnlyList<PokerPlayer> seated,
			IReadOnlyList<(PokerPlayer Player, int RankGroup)> contenders, Dictionary<ulong, int> winnings)
		{
			winnings.Clear();

			var pot = data.Pot.Value;
			data.Pot.Value = 0;
			data.PotItems.Clear();

			if (pot <= 0 || contenders.Count == 0) return;

			var caps = CollectContributionCaps(contenders);

			var paidOut = 0;
			var previousCap = 0;

			foreach (var cap in caps)
			{
				// Everyone at the table feeds the layer, folded players included — their money does not
				// come back just because they let the hand go.
				var layer = 0;
				foreach (var player in seated)
				{
					if (!player || !player.Data) continue;

					layer += Math.Clamp(player.Data.TotalBet.Value - previousCap, 0, cap - previousCap);
				}

				Award(layer, EligibleWinners(contenders, cap), winnings);
				paidOut += layer;
				previousCap = cap;
			}

			// Whatever no layer accounted for — money left behind by players who are gone, or folded
			// bets above what any live hand covered — goes to whoever covered the most.
			var leftover = pot - paidOut;
			if (leftover > 0) Award(leftover, EligibleWinners(contenders, previousCap), winnings);
		}

		private static List<int> CollectContributionCaps(IReadOnlyList<(PokerPlayer Player, int RankGroup)> contenders)
		{
			CapBuffer.Clear();

			foreach (var (player, _) in contenders)
			{
				var contribution = player.Data.TotalBet.Value;
				if (contribution > 0 && !CapBuffer.Contains(contribution)) CapBuffer.Add(contribution);
			}

			CapBuffer.Sort();
			return CapBuffer;
		}

		// The best-ranked hands among those whose contribution reaches the cap. Ties all qualify.
		private static List<PokerPlayer> EligibleWinners(IReadOnlyList<(PokerPlayer Player, int RankGroup)> contenders, int cap)
		{
			WinnerBuffer.Clear();

			var bestGroup = int.MaxValue;

			foreach (var (player, rankGroup) in contenders)
			{
				if (player.Data.TotalBet.Value < cap) continue;
				if (rankGroup > bestGroup) continue;

				if (rankGroup < bestGroup)
				{
					bestGroup = rankGroup;
					WinnerBuffer.Clear();
				}

				WinnerBuffer.Add(player);
			}

			return WinnerBuffer;
		}

		private static void Award(int amount, List<PokerPlayer> winners, Dictionary<ulong, int> winnings)
		{
			if (amount <= 0 || winners.Count == 0) return;

			var share = amount / winners.Count;
			var remainder = amount - share * winners.Count;

			for (var i = 0; i < winners.Count; i++)
			{
				var won = share + (i == 0 ? remainder : 0);
				if (won <= 0) continue;

				winners[i].Data.ServerWinChips(won);

				winnings.TryGetValue(winners[i].ClientId, out var total);
				winnings[winners[i].ClientId] = total + won;
			}
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

		// The completion readout a simultaneous street shows: everyone still in the hand, and how many of
		// them are held to an answer — locked in by having acted, or by having nothing left to act with.
		public static (int LockedIn, int Total) CountLockedIn(IReadOnlyList<PokerPlayer> players)
		{
			var total = 0;
			var lockedIn = 0;

			foreach (var player in players)
			{
				if (!player || !player.Data || !player.Data.IsInHand) continue;

				total++;
				if (!player.Data.CanAct || player.Data.HasActed.Value) lockedIn++;
			}

			return (lockedIn, total);
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
