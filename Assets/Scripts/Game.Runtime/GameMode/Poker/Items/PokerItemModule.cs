using System.Collections.Generic;
using Game.Runtime.GameMode.Config;
using Game.Runtime.GameMode.Poker.Modules;
using Game.Runtime.GameMode.Poker.Player;
using Game.Runtime.GameMode.Poker.Stages;
using Sirenix.OdinInspector;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Items
{
	// Items at this table: dealing them at the start of each hand, deciding who may play one and when, and
	// holding the rules they put on the table. A mode has items by listing this module and loses them by
	// removing it; nothing in the stages knows it exists.
	public class PokerItemModule : PokerModule
	{
		public const string UseCommand = "Item.Use";

		[Header("Items")]
		[Tooltip("What this mode deals and how often. Each mode brings its own.")]
		[Required]
		[SerializeField] private PokerItemDatabase _database;

		[Header("Dealing")]
		[Tooltip("Items every player still in the match is dealt as a hand starts.")]
		[MinValue(0)]
		[SerializeField] private int _itemsPerHand = 2;

		[Tooltip("Extra items for everyone who did not win the previous hand, folders included.")]
		[MinValue(0)]
		[SerializeField] private int _loserBonus = 1;

		[Tooltip("Most items one player can hold. What is dealt beyond it is lost.")]
		[MinValue(1)]
		[SerializeField] private int _capacity = 5;

		[Header("Use")]
		[Tooltip("Items one player may play on a single street.")]
		[MinValue(1)]
		[SerializeField] private int _usesPerStreet = 1;

		// Counts every street opened this session. A rule aimed at "the next street" is aimed at this plus
		// one, and never has to be cleared off a street that has already passed.
		[HideInInspector] public NetworkVariable<int> StreetSerial = new(0,
			readPerm: NetworkVariableReadPermission.Everyone,
			writePerm: NetworkVariableWritePermission.Server);

		public readonly NetworkList<PokerItemTableRule> TableRules = new(null,
			NetworkVariableReadPermission.Everyone,
			NetworkVariableWritePermission.Server);

		// Server only: who did not win the hand just settled, owed a bonus at the next deal.
		private readonly HashSet<ulong> _previousLosers = new();
		private readonly List<PokerItemType> _drawn = new();

		public PokerItemDatabase Database => _database;
		public int Capacity => Mathf.Max(1, _capacity);

		public override void OnNetworkSpawn()
		{
			StreetSerial.OnValueChanged += HandleStreetSerialChanged;
			TableRules.OnListChanged += HandleTableRulesChanged;
		}

		public override void OnNetworkDespawn()
		{
			TableRules.OnListChanged -= HandleTableRulesChanged;
			StreetSerial.OnValueChanged -= HandleStreetSerialChanged;
		}

		private void HandleStreetSerialChanged(int previous, int current) => NotifyRulesChanged();
		private void HandleTableRulesChanged(NetworkListEvent<PokerItemTableRule> change) => NotifyRulesChanged();

		private void NotifyRulesChanged()
		{
			if (GameMode) GameMode.NotifyActionRulesChanged();
		}

		public bool TryGetItem(PokerItemType type, out PokerItem item)
		{
			item = null;
			return _database && _database.TryGetItem(type, out item);
		}

		public PokerItemContext ContextFor(PokerPlayer user) => new(GameMode, this, user);

		// The one answer to "may this player play this item now", for the picker and the server alike.
		public PokerItemAvailability GetAvailability(PokerPlayer user, PokerItemType type)
		{
			if (!TryGetItem(type, out var item)) return PokerItemAvailability.Hidden("This table does not deal it.");
			if (!user || !user.ItemInventory || !user.ItemInventory.Holds(type)) return PokerItemAvailability.Hidden("Not in your hand.");
			if (!GameMode || !GameMode.IsPlayingThisMatch(user.Data)) return PokerItemAvailability.Dimmed("You are out of this match.");

			var street = GameMode.FindStage(Data.StageId.Value.ToString()) as PokerStreetStage;
			if (!street) return PokerItemAvailability.Dimmed("Only on a betting street.");
			if (Data.CurrentTurnClientId.Value != user.ClientId) return PokerItemAvailability.Dimmed("Only on your turn.");
			if (user.ItemInventory.UsesOn(StreetSerial.Value) >= Mathf.Max(1, _usesPerStreet)) return PokerItemAvailability.Dimmed("Already played an item this street.");

			return item.GetAvailability(ContextFor(user));
		}

		public bool IsStreetCurrent => GameMode && GameMode.FindStage(Data.StageId.Value.ToString()) is PokerStreetStage;

		// Whether a street follows the one running now, for items aimed at the next one.
		public bool HasNextStreet()
		{
			if (!GameMode) return false;

			var street = GameMode.FindStage(Data.StageId.Value.ToString());
			return street && GameMode.HasStreetAfter(street);
		}

		public void ServerAddRule(PokerItemTableRuleKind kind, int streetSerial, int amount, ulong sourceClientId)
		{
			if (!IsServer) return;

			TableRules.Add(new PokerItemTableRule
			{
				Kind = kind,
				StreetSerial = streetSerial,
				Amount = amount,
				SourceClientId = sourceClientId
			});
		}

		private bool HasRule(PokerItemTableRuleKind kind, int streetSerial)
		{
			foreach (var rule in TableRules)
			{
				if (rule.Kind == kind && rule.StreetSerial == streetSerial) return true;
			}

			return false;
		}

		private int SumRule(PokerItemTableRuleKind kind, int streetSerial)
		{
			var sum = 0;
			foreach (var rule in TableRules)
			{
				if (rule.Kind == kind && rule.StreetSerial == streetSerial) sum += rule.Amount;
			}

			return sum;
		}

		public override bool IsActionAllowed(PokerPlayerData player, PokerActionType action)
		{
			if (action != PokerActionType.Fold || !IsStreetCurrent) return true;

			return !HasRule(PokerItemTableRuleKind.NoFold, StreetSerial.Value);
		}

		public override int ModifyStakeSize(PokerStreetStage street, int stakeSize) =>
			stakeSize + SumRule(PokerItemTableRuleKind.ExtraStake, StreetSerial.Value);

		public override void OnStageStarting(PokerStage stage)
		{
			if (IsServer && stage is PokerStreetStage) StreetSerial.Value++;
		}

		// After the cards, before the first street: the hand is dealt, and so are the items to play it with.
		public override void OnStageEnded(PokerStage stage)
		{
			if (IsServer && stage is PokerDealStage) ServerDealItems();
		}

		public override void OnHandSettled(IReadOnlyList<PokerPlayer> winners)
		{
			_previousLosers.Clear();

			foreach (var player in GameMode.SeatedPlayers)
			{
				if (!player || !player.Data || Contains(winners, player)) continue;
				if (player.Data.IsInHand || player.Data.IsFolded) _previousLosers.Add(player.ClientId);
			}
		}

		public override void OnHandEnded()
		{
			if (!IsServer) return;

			if (TableRules.Count > 0) TableRules.Clear();

			foreach (var player in PokerPlayer.All)
			{
				if (player && player.ItemKnowledge) player.ItemKnowledge.ServerResetForHand();
			}
		}

		public override void OnMatchEnded()
		{
			if (!IsServer) return;

			_previousLosers.Clear();

			foreach (var player in PokerPlayer.All)
			{
				if (player && player.ItemInventory) player.ItemInventory.ServerResetForMatch();
			}
		}

		public override bool HandleCommandServer(ulong clientId, FixedString32Bytes commandId, int payload)
		{
			if (commandId != UseCommand) return false;

			ServerUse(clientId, (PokerItemType)payload);
			return true;
		}

		private void ServerDealItems()
		{
			if (!_database) return;

			foreach (var player in GameMode.SeatedPlayers)
			{
				if (!player || !player.ItemInventory || !GameMode.IsPlayingThisMatch(player.Data)) continue;

				var bonus = _previousLosers.Contains(player.ClientId) ? _loserBonus : 0;

				_database.Draw(_itemsPerHand + bonus, _drawn);
				foreach (var type in _drawn) player.ItemInventory.ServerGive(type, Capacity);

				if (bonus > 0 && GameMode.Notices) GameMode.Notices.ServerAnnounce(PokerNotice.ForItemBonus(player.ClientId, bonus));
			}

			_previousLosers.Clear();
		}

		private void ServerUse(ulong clientId, PokerItemType type)
		{
			var user = GameMode.FindSeatedPlayer(clientId);
			var availability = GetAvailability(user, type);

			if (!availability.IsUsable)
			{
				Debug.LogWarning($"[{ModuleId}] {type} refused for client {clientId}: {availability.BlockReason}");
				return;
			}

			TryGetItem(type, out var item);

			user.ItemInventory.ServerTake(type);
			user.ItemInventory.ServerRecordUse(StreetSerial.Value);

			if (item.HallucinationCost > 0) user.Data.ServerChangeHallucination(item.HallucinationCost);

			item.UseServer(ContextFor(user));

			if (GameMode.Notices) GameMode.Notices.ServerAnnounce(PokerNotice.ForItemUsed(clientId, type));
		}

		protected override void OnCollectConfigEntries(List<MatchConfigEntry> entries)
		{
			entries.Add(new MatchConfigInt(ModuleId, "Items", "ItemsPerHand", "Items Per Hand", 0, 5, 1,
				() => _itemsPerHand, value => _itemsPerHand = value));
			entries.Add(new MatchConfigInt(ModuleId, "Items", "LoserBonus", "Loser Bonus", 0, 3, 1,
				() => _loserBonus, value => _loserBonus = value));
			entries.Add(new MatchConfigInt(ModuleId, "Items", "Capacity", "Item Capacity", 1, 10, 1,
				() => _capacity, value => _capacity = value));
		}

		private static bool Contains(IReadOnlyList<PokerPlayer> players, PokerPlayer player)
		{
			if (players == null) return false;

			foreach (var candidate in players)
			{
				if (candidate == player) return true;
			}

			return false;
		}
	}
}
