using System;
using System.Collections.Generic;
using Game.Runtime.GameMode.Poker.Items;
using Game.Runtime.GameMode.Poker.Player;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker
{
	// Pure table mechanics shared by the stages — the seat order and every write to the pot live here, so
	// no stage keeps a second copy of either.
	public static class PokerTableUtility
	{
		// Scratch space for the settlement, not state — cleared before every use, reset anyway because
		// statics survive between play sessions with Domain Reload off.
		private static readonly List<(ulong OwnerClientId, PokerItemType ItemType)> ServeBuffer = new();

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void ResetStatics()
		{
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

		public static void ResetPot(PokerGameData data)
		{
			if (data) data.PotItems.Clear();
		}

		// A wagered cap goes onto the table as one entry, stamped with the wager it went up on.
		public static void WagerItem(PokerGameData data, PokerPlayer player, PokerItemType itemType)
		{
			if (!data || !player || !player.Data) return;

			AddPotItem(data, player.ClientId, itemType);
		}

		// The round's own settlement: what the winner put up is what every loser ends up holding, a full copy
		// each rather than a share — the point of choosing a kind is that it is aimed at the whole table. A
		// player who folded escapes that and keeps only their own opening cap, which is what folding costs.
		// Nothing is ever won: the losers' own caps are simply gone, and so are the winner's.
		//
		// The caps change hands rather than being copied onto a second list. Everything on the table comes
		// off and goes back on through the same AddPotItem the wager uses, stamped with its new owner — so
		// a settled cap is spawned by exactly the path that spawns a staked one, lands on the same seat
		// anchor, and is caught by everything watching the table. A plate of its own was a second set of
		// objects nobody else knew about, and a hallucination painting the caps found half of them.
		//
		// Swapped rather than swallowed. The effects used to land in the same frame the showdown resolved, so
		// a round's entire consequence happened behind the ranking board and nobody saw it — the caps change
		// owner here and PokerItemConsumeStage is where they actually go down.
		public static void SwapPotToLosers(PokerGameData data, PokerPlayer winner, IReadOnlyList<PokerPlayer> players,
			PokerItemDatabase database, PokerPhase foldPhase)
		{
			if (!data || database == null) { ResetPot(data); return; }

			// Decided against the pot as it stands, before a word of it is rewritten: the swap reads what
			// everybody staked and the re-add is what changes it.
			ServeBuffer.Clear();

			foreach (var player in players)
			{
				if (!player || !player.Data) continue;
				if (winner && player == winner) continue;
				// Only a player dealt into this hand owes anything: still in it or folded out of it. A chair taken
				// after the deal is Waiting, and one who went under is Dead — neither played the hand being settled.
				var folded = player.Data.IsFolded;
				if (!player.Data.IsInHand && !folded) continue;

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

			foreach (var (ownerClientId, itemType) in ServeBuffer) AddPotItem(data, ownerClientId, itemType);

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
				return true;
			}

			return false;
		}

		// One entry per cap, stamped with who it stands in front of, on which wager and what it is.
		private static void AddPotItem(PokerGameData data, ulong ownerClientId, PokerItemType itemType)
		{
			data.PotItems.Add(new PokerBetItem
			{
				OwnerClientId = ownerClientId,
				Phase = data.Phase.Value,
				ItemType = itemType
			});
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
	}
}
