using System;
using System.Collections.Generic;
using Game.Runtime.GameMode.Poker.BetItems;
using Game.Runtime.GameMode.Poker.Hallucination;
using Game.Runtime.GameMode.Poker.Hands;
using Game.Runtime.GameMode.Poker.Items;
using Game.Runtime.GameMode.Poker.Player;
using Game.Runtime.GameMode.Poker.Stages;
using Game.Runtime.Player;
using Game.Runtime.Steam;
using Unity.Netcode;

namespace Game.Runtime.GameMode.Poker.Modules
{
	// Reads the table on the server and tells each player which achievements they just earned; their own client
	// unlocks them through AchievementController. Bots earn nothing.
	public class PokerAchievementModule : PokerModule
	{
		private const int HighRate = 80;
		private const int SurvivorBetItems = 10;

		private static readonly int EveryItemMask = BuildEveryItemMask();

		private PokerItemModule _items;

		// Server only: what has been sent, so a repeat never travels.
		private readonly HashSet<(ulong, AchievementId)> _granted = new();

		// Match: caps each player ate, who put a player under with an item.
		private readonly Dictionary<ulong, int> _betItemsEaten = new();
		private readonly Dictionary<ulong, ulong> _itemKillers = new();

		// Hand: the last street it reached, the streets each player used an item on, who failed a sniff.
		private int _handStreets;
		private readonly Dictionary<ulong, HashSet<int>> _itemStreets = new();
		private readonly HashSet<ulong> _usedItem = new();
		private readonly HashSet<ulong> _failedSniff = new();

		// The item resolving now, and who went under while it did.
		private ulong _itemUser = PokerGameData.NoTurn;
		private ulong _itemTarget = PokerGameData.NoTurn;
		private PokerItem _item;
		private readonly HashSet<ulong> _wentUnderDuringItem = new();

		protected override void OnInitialize()
		{
			if (!IsServer) return;

			_items = GameMode.FindModule<PokerItemModule>();
			if (_items)
			{
				_items.OnItemUsed += HandleItemUsed;
				_items.OnItemResolved += HandleItemResolved;
			}

			Data.OnPotEntriesChanged += HandlePotEntriesChanged;
			PokerPlayerData.OnServerWentUnder += HandleWentUnder;
			PokerHallucinationRollController.OnServerRollPaid += HandleRollPaid;
		}

		protected override void OnDeInitialize()
		{
			PokerHallucinationRollController.OnServerRollPaid -= HandleRollPaid;
			PokerPlayerData.OnServerWentUnder -= HandleWentUnder;
			if (Data) Data.OnPotEntriesChanged -= HandlePotEntriesChanged;

			if (_items)
			{
				_items.OnItemResolved -= HandleItemResolved;
				_items.OnItemUsed -= HandleItemUsed;
			}

			_items = null;
		}

		public override void OnStageStarting(PokerStage stage)
		{
			if (stage is not PokerDealStage) return;

			// The hand before is over, its eating included.
			GrantSurvivors();
			ResetHand();
		}

		public override void OnStageStarted(PokerStage stage)
		{
			_handStreets = Math.Max(_handStreets, Data.StreetNumber.Value);
		}

		public override void OnHandSettled(IReadOnlyList<PokerPlayer> winners)
		{
			foreach (var winner in winners)
			{
				if (!winner || !winner.Data) continue;

				var id = winner.ClientId;
				var result = GameMode.EvaluateHand(winner.Data);

				if (result.HandType is PokerHandFiveOfAKind) Grant(id, AchievementId.THE_ULTIMATE_HAND);
				if (result.HandType is PokerHandRoyalFlush) Grant(id, AchievementId.THE_ROYAL_FAMILY);
				if (winners.Count == 1 && IsWeakerThanEveryFolder(winner, result)) Grant(id, AchievementId.SKILL_ISSUE);

				if (!_items) continue;

				if (!_usedItem.Contains(id)) Grant(id, AchievementId.LUCK_ISSUE);
				else if (_handStreets > 0 && _itemStreets.TryGetValue(id, out var streets) && streets.Count >= _handStreets) Grant(id, AchievementId.STRATEGIST);
			}
		}

		// Read before the match reset puts the rates and the stamps back.
		public override void OnMatchEnded()
		{
			GrantSurvivors();

			var survivor = PokerPlayer.Find(Data.SurvivorClientId.Value);
			if (survivor && survivor.Data && survivor.Data.IsAlive)
			{
				var id = survivor.ClientId;

				Grant(id, AchievementId.LAST_GNOME_STANDING);
				if (survivor.Data.HallucinationRate.Value > HighRate) Grant(id, AchievementId.TRIP_MAN_WINNING);
				if (KilledEveryoneWithItems(id)) Grant(id, AchievementId.NO_CARD_NEEDED);
			}

			ResetHand();
			_betItemsEaten.Clear();
			_itemKillers.Clear();
		}

		private void HandlePotEntriesChanged(NetworkListEvent<PokerPotEntry> change)
		{
			// A cap leaving the pot on its own is being eaten; settlements clear and re-add.
			if (change.Type != NetworkListEvent<PokerPotEntry>.EventType.RemoveAt) return;
			if (change.Value.BetItemType == PokerBetItemDatabase.PlainChip) return;

			var id = change.Value.OwnerClientId;
			_betItemsEaten[id] = _betItemsEaten.GetValueOrDefault(id) + 1;

			Grant(id, AchievementId.JUST_A_TASTE);
		}

		private void HandleItemUsed(ulong user, PokerItemType type, ulong target, int outcome)
		{
			_itemUser = user;
			_itemTarget = target;
			_items.TryGetItem(type, out _item);
			_wentUnderDuringItem.Clear();

			_usedItem.Add(user);
			var street = Data.StreetNumber.Value;
			if (street > 0) StreetsOf(user).Add(street);

			if (_item is PokerItemRateShift && outcome == PokerItemRateShift.Raised && target != PokerGameData.NoTurn) _failedSniff.Add(target);

			Grant(user, AchievementId.JUST_TESTING);

			if (IsPerson(user)) RecordItemUsedRPC(type, RpcTarget.Single(user, RpcTargetUse.Temp));
		}

		private void HandleItemResolved(ulong user, PokerItemType type)
		{
			if (_item is PokerItemSharedRoll && _wentUnderDuringItem.Contains(user) && _wentUnderDuringItem.Contains(_itemTarget))
			{
				Grant(user, AchievementId.CHEERS_TO_THE_GRAVE);
				Grant(_itemTarget, AchievementId.CHEERS_TO_THE_GRAVE);
			}

			var player = PokerPlayer.Find(user);
			if (_item is PokerItemTableReveal or PokerItemPeekBoard && SawJoker(player)) Grant(user, AchievementId.FOUND_THE_CLOWN);

			GrantIfTwoJokers(player);
			GrantIfTwoJokers(PokerPlayer.Find(_itemTarget));

			_itemUser = PokerGameData.NoTurn;
			_itemTarget = PokerGameData.NoTurn;
			_item = null;
			_wentUnderDuringItem.Clear();
		}

		private void HandleWentUnder(PokerPlayerData data)
		{
			var id = data.OwnerClientId;

			if (_itemUser != PokerGameData.NoTurn)
			{
				_wentUnderDuringItem.Add(id);
				if (id != _itemUser) _itemKillers[id] = _itemUser;
			}

			// A roll's death is told by HandleRollPaid; anything else that filled the bar is an overdose.
			var player = data.GetComponent<PokerPlayer>();
			if (player && player.HallucinationRoll && player.HallucinationRoll.ServerIsPayingFatalRoll) return;

			Grant(id, AchievementId.OVERDOSE);
			if (_failedSniff.Contains(id)) Grant(id, AchievementId.BAD_TRIP);
		}

		private void HandleRollPaid(PokerHallucinationRollController roller, int against, bool fatal)
		{
			var id = roller.OwnerClientId;

			if (!fatal)
			{
				if (against > HighRate) Grant(id, AchievementId.DENIED_DEATH);
				return;
			}

			Grant(id, AchievementId.TRIPPED_ON_A_TRIP);
			if (_item is PokerItemHalfDose && id == _itemTarget) Grant(id, AchievementId.CANNOT_BE_SAVED);
		}

		private void GrantSurvivors()
		{
			foreach (var (id, eaten) in _betItemsEaten)
			{
				var player = PokerPlayer.Find(id);
				if (eaten > SurvivorBetItems && player && player.Data && player.Data.IsAlive) Grant(id, AchievementId.SURVIVOR);
			}
		}

		// Won with everybody else folded, holding less than every one of them was.
		private bool IsWeakerThanEveryFolder(PokerPlayer winner, PokerHandResult result)
		{
			var folders = 0;

			foreach (var player in GameMode.SeatedPlayers)
			{
				if (!player || player == winner || !player.Data || !player.Data.InMatch.Value) continue;
				if (player.Data.IsInHand) return false;
				if (!player.Data.IsFolded || player.Data.CardCount == 0) continue;

				if (result.CompareTo(GameMode.EvaluateHand(player.Data)) >= 0) return false;
				folders++;
			}

			return folders > 0;
		}

		private bool KilledEveryoneWithItems(ulong survivor)
		{
			var victims = 0;

			foreach (var player in GameMode.SeatedPlayers)
			{
				if (!player || player.ClientId == survivor || !player.Data || !player.Data.InMatch.Value) continue;
				if (!_itemKillers.TryGetValue(player.ClientId, out var killer) || killer != survivor) return false;

				victims++;
			}

			return victims > 0;
		}

		// A Joker seen in somebody else's hand over their head, or on a board card only this player was shown.
		private bool SawJoker(PokerPlayer user)
		{
			if (!user) return false;

			foreach (var player in GameMode.SeatedPlayers)
			{
				if (!player || player == user || !player.Data) continue;

				for (var slot = 0; slot < player.Data.CardCount; slot++)
				{
					if (player.Data.IsHoleCardShown(slot) && player.Data.HoleCards[slot].IsJoker) return true;
				}
			}

			var knowledge = user.ItemKnowledge;
			if (!knowledge) return false;

			for (var slot = 0; slot < Data.CommunityCards.Count; slot++)
			{
				if (knowledge.Knows(PokerGameData.NoTurn, slot) && Data.CommunityCards[slot].IsJoker) return true;
			}

			return false;
		}

		private void GrantIfTwoJokers(PokerPlayer player)
		{
			if (!player || !player.Data) return;

			var jokers = 0;
			foreach (var card in player.Data.HoleCards)
			{
				if (card.IsJoker) jokers++;
			}

			if (jokers >= 2) Grant(player.ClientId, AchievementId.LUCK_OR_SKILL);
		}

		private HashSet<int> StreetsOf(ulong id)
		{
			if (!_itemStreets.TryGetValue(id, out var streets)) _itemStreets[id] = streets = new HashSet<int>();
			return streets;
		}

		private void ResetHand()
		{
			_handStreets = 0;
			_itemStreets.Clear();
			_usedItem.Clear();
			_failedSniff.Clear();
		}

		private static bool IsPerson(ulong id) => id != PokerGameData.NoTurn && !PlayerBot.IsBot(id);

		private void Grant(ulong id, AchievementId achievement)
		{
			if (!IsServer || !IsSpawned || !IsPerson(id) || !_granted.Add((id, achievement))) return;

			UnlockRPC(achievement, RpcTarget.Single(id, RpcTargetUse.Temp));
		}

		[Rpc(SendTo.SpecifiedInParams)]
		private void UnlockRPC(AchievementId achievement, RpcParams rpcParams = default) => AchievementController.Unlock(achievement);

		// Kept as a Steam stat, one bit per item type, since trying every item spans matches.
		[Rpc(SendTo.SpecifiedInParams)]
		private void RecordItemUsedRPC(PokerItemType type, RpcParams rpcParams = default)
		{
			var used = AchievementController.GetStat(AchievementStat.ITEM_TYPES_USED);
			var next = used | (1 << (int)type);
			if (next != used) AchievementController.SetStat(AchievementStat.ITEM_TYPES_USED, next);

			if ((next & EveryItemMask) == EveryItemMask) AchievementController.Unlock(AchievementId.COMPLETIONIST);
		}

		private static int BuildEveryItemMask()
		{
			var mask = 0;
			foreach (PokerItemType type in Enum.GetValues(typeof(PokerItemType)))
			{
				if (type != PokerItemType.None) mask |= 1 << (int)type;
			}

			return mask;
		}
	}
}
