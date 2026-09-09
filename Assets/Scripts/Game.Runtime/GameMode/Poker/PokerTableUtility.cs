using System;
using System.Collections.Generic;
using Game.Runtime.GameMode.Poker.Items;
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
		private static readonly List<PokerItemType> TypeBuffer = new();
		private static readonly List<(ulong OwnerClientId, PokerItemType ItemType)> ServeBuffer = new();

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void ResetStatics()
		{
			CapBuffer.Clear();
			WinnerBuffer.Clear();
			TypeBuffer.Clear();
			ServeBuffer.Clear();
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

		// A wagered cap goes onto the plate as one unit. No money moves: a wager in this round has no size,
		// so Pot is a count of caps rather than a sum, and the ledger beside it is what says which kinds are
		// there to be eaten. Written here with every other pot write, so the scalar and the ledger cannot
		// drift apart.
		public static void WagerItem(PokerGameData data, PokerPlayer player, PokerItemType itemType)
		{
			if (!data || !player || !player.Data) return;

			TypeBuffer.Clear();
			TypeBuffer.Add(itemType);
			AddPotItems(data, player.ClientId, TypeBuffer);

			data.Pot.Value += 1;
		}

		// The round's own settlement: what the winner put up is what every loser ends up holding, a full copy
		// each rather than a share — the point of choosing a kind is that it is aimed at the whole table. A
		// player who folded escapes that and keeps only their own opening cap, which is what folding costs.
		// Nothing is ever won: the losers' own caps are simply gone, and so are the winner's.
		//
		// The caps change hands rather than being copied onto a second list. Everything on the table comes
		// off and goes back on through the same AddPotItems the wager uses, stamped with its new owner — so
		// a settled cap is spawned by exactly the path that spawns a staked one, lands on the same seat
		// anchor, and is caught by everything watching the table. A plate of its own was a second set of
		// objects nobody else knew about, and a hallucination painting the caps found half of them.
		//
		// Swapped rather than swallowed. The effects used to land in the same frame the showdown resolved, so
		// a round's entire consequence happened behind the ranking board and nobody saw it — the caps change
		// owner here and PokerItemConsumeStage is where they actually go down.
		public static void SwapPotToLosers(PokerGameData data, PokerPlayer winner, IReadOnlyList<PokerPlayer> players,
			PokerItemDatabase database, PokerPhase foldPhase, Modules.PokerAbilityModule abilities = null)
		{
			if (!data || database == null) { ResetPot(data); return; }

			// Decided against the pot as it stands, before a word of it is rewritten: the swap reads what
			// everybody staked and the re-add is what changes it.
			ServeBuffer.Clear();

			foreach (var player in players)
			{
				if (!player || !player.Data) continue;
				if (winner && player == winner) continue;
				if (!player.Data.IsSeated) continue;

				// Losing is what earns an item, folding included: coming last is the round's own reward, and
				// a table where only the eaters are compensated punishes folding twice.
				abilities?.GiveLossRewardServer(player);

				var folded = player.Data.Status.Value == PokerPlayerStatus.Folded;

				for (var i = 0; i < data.PotItems.Count; i++)
				{
					var item = data.PotItems[i];

					// A folder keeps their own opening cap and nothing else; everyone still in takes every cap
					// the winner put up.
					var theirs = folded
						? item.OwnerClientId == player.ClientId && item.Phase == foldPhase
						: winner && item.OwnerClientId == winner.ClientId;

					if (!theirs) continue;

					ServeBuffer.Add((player.ClientId, item.ItemType));
				}
			}

			// Cleared first, so every cap on the table comes off — the winner's included, and anything nobody
			// was served with it.
			ResetPot(data);

			foreach (var (ownerClientId, itemType) in ServeBuffer)
			{
				TypeBuffer.Clear();
				TypeBuffer.Add(itemType);
				AddPotItems(data, ownerClientId, TypeBuffer);

				data.Pot.Value += 1;
			}

			ServeBuffer.Clear();
		}

		// How much of the pot is standing in front of one player. What is left to eat, once the settlement
		// has handed the caps round.
		public static int CountPotItems(PokerGameData data, ulong ownerClientId)
		{
			if (!data) return 0;

			var count = 0;
			for (var i = 0; i < data.PotItems.Count; i++)
			{
				if (data.PotItems[i].OwnerClientId == ownerClientId) count++;
			}

			return count;
		}

		// The next bite, taken off the table as it goes down rather than after — the ledger is what the
		// visual draws, so a cap still on it whose effect has already landed is one the table can see that
		// nobody is going to eat.
		public static bool ServerTakePotItem(PokerGameData data, ulong ownerClientId, out PokerItemType itemType)
		{
			itemType = PokerItemDatabase.PlainChip;
			if (!data) return false;

			for (var i = 0; i < data.PotItems.Count; i++)
			{
				if (data.PotItems[i].OwnerClientId != ownerClientId) continue;

				itemType = data.PotItems[i].ItemType;
				data.PotItems.RemoveAt(i);
				data.Pot.Value = Mathf.Max(0, data.Pot.Value - 1);
				return true;
			}

			return false;
		}

		// The pot's itemised half. One entry per unit staked, stamped with who fed it, on which street,
		// and what it is — the types were drawn off the front of the owner's wallet when the stake was
		// placed. Only ever written beside the scalar, in this class, so the two cannot disagree.
		private static void AddPotItems(PokerGameData data, ulong ownerClientId, List<PokerItemType> itemTypes)
		{
			foreach (var itemType in itemTypes)
			{
				data.PotItems.Add(new PokerBetItem
				{
					OwnerClientId = ownerClientId,
					Phase = data.Phase.Value,
					ItemType = itemType
				});
			}
		}

		// The loser-eats settlement: the pot is not won, it is swallowed. Every unit goes down the
		// eaters' throats in pot order — ties share the plate unit by unit — and each one does whatever
		// its kind does. Nothing is ever paid out: winning here is worth exactly not having to eat,
		// the same shape as the report's "an accusation costs the loser; it never pays the winner".
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
