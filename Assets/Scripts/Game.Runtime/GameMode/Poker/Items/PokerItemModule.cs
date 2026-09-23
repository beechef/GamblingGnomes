using System;
using System.Collections.Generic;
using System.Threading;
using Game.Runtime.GameMode.Config;
using Game.Runtime.GameMode.Poker.Modules;
using Game.Runtime.GameMode.Poker.Player;
using Game.Runtime.GameMode.Poker.Stages;
using Sirenix.OdinInspector;
using Unity.Netcode;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Items
{
	// Items at this table: dealing them at the start of each hand, deciding who may play one and when, holding
	// the rules they put on the table, and waiting on a player an item asks to answer. A mode has items by
	// listing this module and loses them by removing it; nothing in the stages knows it exists.
	public class PokerItemModule : PokerModule
	{
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

		[Tooltip("Testing only: given to every player in the match at its first deal, ahead of the random draw. Ignored outside the editor and development builds.")]
		[InfoBox("Some of these are not in the database and will not be given.", InfoMessageType.Warning, nameof(HasUndealtStartingItems))]
		[SerializeField] private List<PokerItemType> _startingItems = new();

		[FoldoutGroup("Testing"), ShowInInspector, DisableInEditorMode]
		private PokerItemType _giveItem = PokerItemType.DeckCount;

		[FoldoutGroup("Testing"), ShowInInspector, DisableInEditorMode, ValueDropdown(nameof(GiveTargets))]
		private ulong _giveTarget = PokerGameData.NoTurn;

		[Header("Use")]
		[Tooltip("Items one player may play on a single street.")]
		[MinValue(1)]
		[SerializeField] private int _usesPerStreet = 1;

		[Tooltip("Seconds a player an item asks to choose has to answer before the table chooses for them. Never longer than the user's turn has left.")]
		[MinValue(1f)]
		[SerializeField] private float _responseDuration = 10f;

		[Tooltip("How long two cards changing places are in the air, read here and by every screen flying them.")]
		[Required]
		[SerializeField] private PokerCardExchangePacing _exchangePacing;

		// Counts every street opened this session. A rule aimed at "the next street" is aimed at this plus
		// one, and never has to be cleared off a street that has already passed.
		[HideInInspector] public NetworkVariable<int> StreetSerial = new(0,
			readPerm: NetworkVariableReadPermission.Everyone,
			writePerm: NetworkVariableWritePermission.Server);

		public readonly NetworkList<PokerItemTableRule> TableRules = new(null,
			NetworkVariableReadPermission.Everyone,
			NetworkVariableWritePermission.Server);

		// Who the table is waiting on to answer an item, if anybody.
		[HideInInspector] public NetworkVariable<PokerItemResponse> PendingResponse = new(PokerItemResponse.None,
			readPerm: NetworkVariableReadPermission.Everyone,
			writePerm: NetworkVariableWritePermission.Server);

		// Raised on every peer when the table starts or stops waiting on somebody.
		public event Action OnPendingResponseChanged;

		// Raised on every peer as two cards set off to change places. The swap itself is written once they land.
		public event Action<PokerCardPlace, PokerCardPlace> OnCardsExchanging;

		public PokerCardExchangePacing ExchangePacing => _exchangePacing;

		// Server only: who did not win the hand just settled, owed a bonus at the next deal.
		private readonly HashSet<ulong> _previousLosers = new();
		private readonly List<PokerItemType> _drawn = new();

		// Server only: who has had the starting items this match.
		private readonly HashSet<ulong> _startingItemsGiven = new();

		// Server only: an item is being resolved, and the answer it is waiting on.
		private bool _resolving;
		private int _responseSlot = -1;

		public PokerItemDatabase Database => _database;
		public int Capacity => Mathf.Max(1, _capacity);
		public float ResponseDuration => Mathf.Max(1f, _responseDuration);

		public override void OnNetworkSpawn()
		{
			StreetSerial.OnValueChanged += HandleStreetSerialChanged;
			TableRules.OnListChanged += HandleTableRulesChanged;
			PendingResponse.OnValueChanged += HandlePendingResponseChanged;
		}

		public override void OnNetworkDespawn()
		{
			PendingResponse.OnValueChanged -= HandlePendingResponseChanged;
			TableRules.OnListChanged -= HandleTableRulesChanged;
			StreetSerial.OnValueChanged -= HandleStreetSerialChanged;
		}

		private void HandleStreetSerialChanged(int previous, int current) => NotifyRulesChanged();
		private void HandleTableRulesChanged(NetworkListEvent<PokerItemTableRule> change) => NotifyRulesChanged();

		private void HandlePendingResponseChanged(PokerItemResponse previous, PokerItemResponse current)
		{
			OnPendingResponseChanged?.Invoke();
			NotifyRulesChanged();
		}

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
			if (!IsStreetCurrent) return PokerItemAvailability.Dimmed("Only on a betting street.");
			if (Data.CurrentTurnClientId.Value != user.ClientId) return PokerItemAvailability.Dimmed("Only on your turn.");
			if (user.ItemInventory.UsesOn(StreetSerial.Value) >= Mathf.Max(1, _usesPerStreet)) return PokerItemAvailability.Dimmed("Already played an item this street.");
			if (PendingResponse.Value.IsPending) return PokerItemAvailability.Dimmed("Waiting on another item.");

			if (item.NeedsResponse && Data.HasTurnClock && Data.TurnRemaining < ResponseDuration)
				return PokerItemAvailability.Dimmed("Not enough time left for them to answer.");

			return item.GetAvailability(ContextFor(user));
		}

		public bool IsStreetCurrent => GameMode && Data && GameMode.FindStage(Data.StageId.Value.ToString()) is PokerStreetStage;
		private bool IsAllInCurrent => GameMode && Data && GameMode.FindStage(Data.StageId.Value.ToString()) is PokerAllInStage;

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

		public void ServerTell(ulong clientId, PokerNotice notice)
		{
			if (GameMode && GameMode.Notices) GameMode.Notices.ServerTell(clientId, notice);
		}

		private bool HasRule(PokerItemTableRuleKind kind, int streetSerial, ulong sourceClientId = PokerGameData.NoTurn)
		{
			foreach (var rule in TableRules)
			{
				if (rule.Kind != kind || rule.StreetSerial != streetSerial) continue;
				if (sourceClientId == PokerGameData.NoTurn || rule.SourceClientId == sourceClientId) return true;
			}

			return false;
		}

		// Rules are cleared when the hand ends, so any one still on the table is this hand's.
		private bool HasRuleForHand(PokerItemTableRuleKind kind, ulong sourceClientId)
		{
			foreach (var rule in TableRules)
			{
				if (rule.Kind == kind && rule.SourceClientId == sourceClientId) return true;
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
			if (action != PokerActionType.Fold) return true;

			var serial = StreetSerial.Value;

			if (player && HasRuleForHand(PokerItemTableRuleKind.NoFoldSelfForHand, player.OwnerClientId)) return false;

			if (IsStreetCurrent)
			{
				if (HasRule(PokerItemTableRuleKind.NoFold, serial)) return false;
				if (player && HasRule(PokerItemTableRuleKind.NoFoldSelf, serial, player.OwnerClientId)) return false;
			}

			if (IsAllInCurrent && HasRule(PokerItemTableRuleKind.NoFoldAllIn, serial)) return false;

			return true;
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
			_startingItemsGiven.Clear();

			foreach (var player in PokerPlayer.All)
			{
				if (player && player.ItemInventory) player.ItemInventory.ServerResetForMatch();
			}
		}

		[Rpc(SendTo.Server)]
		public void UseItemRPC(PokerItemUseRequest request, RpcParams rpcParams = default)
		{
			// Left unawaited on purpose: it carries the module's own lifetime and finishes on its own.
			_ = ServerUseAsync(rpcParams.Receive.SenderClientId, request, destroyCancellationToken);
		}

		private void ServerDealItems()
		{
			if (!_database) return;

			foreach (var player in GameMode.SeatedPlayers)
			{
				if (!player || !player.ItemInventory || !GameMode.IsPlayingThisMatch(player.Data)) continue;

				ServerGiveStartingItems(player);

				var bonus = _previousLosers.Contains(player.ClientId) ? _loserBonus : 0;

				_database.Draw(_itemsPerHand + bonus, _drawn);
				foreach (var type in _drawn) player.ItemInventory.ServerGive(type, Capacity);

				if (bonus > 0 && GameMode.Notices) GameMode.Notices.ServerAnnounce(PokerNotice.ForItemBonus(player.ClientId, bonus));
			}

			_previousLosers.Clear();
		}

		private void ServerGiveStartingItems(PokerPlayer player)
		{
			if (!Debug.isDebugBuild || _startingItems.Count == 0 || !_startingItemsGiven.Add(player.ClientId)) return;

			foreach (var type in _startingItems) ServerGiveItem(player, type);
		}

		// Puts one item in a player's hand outside the deal. Refused past capacity or for an item this table
		// does not deal.
		public bool ServerGiveItem(PokerPlayer player, PokerItemType type)
		{
			if (!IsServer || !player || !player.ItemInventory || !TryGetItem(type, out _)) return false;

			return player.ItemInventory.ServerGive(type, Capacity);
		}

		[FoldoutGroup("Testing"), Button("Give Item"), DisableInEditorMode]
		private void GiveItemForTesting()
		{
			if (!IsServer)
			{
				Debug.LogWarning($"[{ModuleId}] Give Item refused: only the server gives items.");
				return;
			}

			foreach (var player in GameMode.SeatedPlayers)
			{
				if (!player || (_giveTarget != PokerGameData.NoTurn && player.ClientId != _giveTarget)) continue;
				if (!ServerGiveItem(player, _giveItem)) Debug.LogWarning($"[{ModuleId}] Give Item: {_giveItem} refused for {player.DisplayName} (full, or not dealt here).");
			}
		}

		private IEnumerable<ValueDropdownItem<ulong>> GiveTargets()
		{
			yield return new ValueDropdownItem<ulong>("Everyone", PokerGameData.NoTurn);

			if (!GameMode) yield break;

			foreach (var player in GameMode.SeatedPlayers)
			{
				if (player) yield return new ValueDropdownItem<ulong>($"{player.DisplayName} ({player.ClientId})", player.ClientId);
			}
		}

		private bool HasUndealtStartingItems()
		{
			foreach (var type in _startingItems)
			{
				if (!TryGetItem(type, out _)) return true;
			}

			return false;
		}

		private async Awaitable ServerUseAsync(ulong clientId, PokerItemUseRequest request, CancellationToken ct)
		{
			var user = GameMode.FindSeatedPlayer(clientId);
			var availability = GetAvailability(user, request.Item);

			if (!availability.IsUsable)
			{
				Debug.LogWarning($"[{ModuleId}] {request.Item} refused for client {clientId}: {availability.BlockReason}");
				return;
			}

			if (_resolving)
			{
				Debug.LogWarning($"[{ModuleId}] {request.Item} refused for client {clientId}: another item is still resolving.");
				return;
			}

			TryGetItem(request.Item, out var item);
			var context = ContextFor(user);

			if (!item.IsValidRequest(context, request))
			{
				Debug.LogWarning($"[{ModuleId}] {request.Item} refused for client {clientId}: the target does not fit the item.");
				return;
			}

			_resolving = true;

			try
			{
				user.ItemInventory.ServerTake(request.Item);
				user.ItemInventory.ServerRecordUse(StreetSerial.Value);

				if (item.HallucinationCost > 0) user.Data.ServerChangeHallucination(item.HallucinationCost);

				var target = GameMode.FindSeatedPlayerAtSeat(request.TargetSeat);
				if (GameMode.Notices)
					GameMode.Notices.ServerAnnounce(PokerNotice.ForItemUsed(clientId, request.Item, target ? target.ClientId : PokerGameData.NoTurn));

				await item.UseServerAsync(context, request, ct);
			}
			catch (OperationCanceledException)
			{
			}
			catch (Exception exception)
			{
				Debug.LogException(exception, this);
			}
			finally
			{
				_resolving = false;
			}
		}

		// Asks a player to put one of their own cards forward, for an item somebody else played. Answers with the
		// slot they chose, or one drawn from what they could have chosen once the time runs out — which is never
		// later than the user's turn has left.
		public async Awaitable<int> ServerAskForCardAsync(PokerItemContext context, PokerItem item, PokerPlayer responder, CancellationToken ct)
		{
			if (!IsServer || !responder) return -1;

			var now = NetworkManager.ServerTime.Time;
			var duration = ResponseDuration;
			if (Data.HasTurnClock) duration = Mathf.Min(duration, Data.TurnRemaining);

			_responseSlot = -1;
			PendingResponse.Value = new PokerItemResponse
			{
				ResponderClientId = responder.ClientId,
				RequesterClientId = context.User ? context.User.ClientId : PokerGameData.NoTurn,
				Item = item.Type,
				EndTime = now + duration,
				Duration = duration
			};

			try
			{
				while (_responseSlot < 0 && NetworkManager.ServerTime.Time < PendingResponse.Value.EndTime)
				{
					await Awaitable.NextFrameAsync(ct);
				}
			}
			finally
			{
				if (IsSpawned) PendingResponse.Value = PokerItemResponse.None;
			}

			if (_responseSlot >= 0) return _responseSlot;

			return PickRandomSlot(responder.Data.CardCount, slot => item.AcceptsResponseCard(context, responder, slot));
		}

		[Rpc(SendTo.Server)]
		public void RespondWithCardRPC(int slot, RpcParams rpcParams = default)
		{
			var sender = rpcParams.Receive.SenderClientId;
			var pending = PendingResponse.Value;

			if (!pending.IsPending || pending.ResponderClientId != sender)
			{
				Debug.LogWarning($"[{ModuleId}] Card answer from client {sender} refused: nobody asked them.");
				return;
			}

			var responder = GameMode.FindSeatedPlayer(sender);
			var requester = GameMode.FindSeatedPlayer(pending.RequesterClientId);

			if (!TryGetItem(pending.Item, out var item) || !responder || !item.AcceptsResponseCard(ContextFor(requester), responder, slot))
			{
				Debug.LogWarning($"[{ModuleId}] Card answer from client {sender} refused: slot {slot} is not one they may put forward.");
				return;
			}

			_responseSlot = slot;
		}

		// Two cards change places. Every screen is told first and flies them; the lists are written once the
		// flight is over, so no face changes in plain sight. Whatever anybody knew about either card is
		// forgotten, because the slot now holds something else. False when either card was gone by then.
		public async Awaitable<bool> ServerExchangeCardsAsync(PokerCardPlace first, PokerCardPlace second, CancellationToken ct)
		{
			if (!IsServer || !TryReadCard(first, out _) || !TryReadCard(second, out _)) return false;

			PlayCardExchangeRPC(first, second);

			if (_exchangePacing) await Awaitable.WaitForSecondsAsync(_exchangePacing.FlightDuration, ct);

			if (!TryReadCard(first, out var firstCard) || !TryReadCard(second, out var secondCard)) return false;

			WriteCard(first, secondCard);
			WriteCard(second, firstCard);

			ServerForget(first);
			ServerForget(second);

			return true;
		}

		[Rpc(SendTo.Everyone)]
		private void PlayCardExchangeRPC(PokerCardPlace first, PokerCardPlace second) => OnCardsExchanging?.Invoke(first, second);

		public bool TryReadCard(PokerCardPlace place, out CardData card)
		{
			card = CardData.None;

			if (place.IsBoard)
			{
				if (!Data || place.Slot < 0 || place.Slot >= Data.CommunityCards.Count) return false;

				card = Data.CommunityCards[place.Slot];
				return true;
			}

			var holder = GameMode ? GameMode.FindSeatedPlayer(place.HolderClientId) : null;
			if (!holder || !holder.Data || !holder.Data.IsInHand || place.Slot < 0 || place.Slot >= holder.Data.CardCount) return false;

			card = holder.Data.HoleCards[place.Slot];
			return true;
		}

		private void WriteCard(PokerCardPlace place, CardData card)
		{
			if (place.IsBoard)
			{
				GameMode.ServerReplaceCommunityCard(place.Slot, card);
				return;
			}

			var holder = GameMode.FindSeatedPlayer(place.HolderClientId);
			if (holder) holder.Data.ServerReplaceHoleCard(place.Slot, card);
		}

		private static void ServerForget(PokerCardPlace place)
		{
			foreach (var player in PokerPlayer.All)
			{
				if (!player || !player.ItemKnowledge) continue;

				player.ItemKnowledge.ServerForgetCard(place.HolderClientId, place.Slot);
				if (player.ClientId == place.HolderClientId) player.ItemKnowledge.ServerForgetExposure(place.Slot);
			}
		}

		public static int PickRandomSlot(int count, Func<int, bool> accept)
		{
			var valid = 0;
			for (var slot = 0; slot < count; slot++)
			{
				if (accept(slot)) valid++;
			}

			if (valid == 0) return -1;

			var pick = UnityEngine.Random.Range(0, valid);
			for (var slot = 0; slot < count; slot++)
			{
				if (!accept(slot)) continue;
				if (pick-- == 0) return slot;
			}

			return -1;
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
