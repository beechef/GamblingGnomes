using System;
using System.Collections.Generic;
using System.Threading;
using Game.Runtime.GameMode.Config;
using Game.Runtime.GameMode.Poker.Modules;
using Game.Runtime.GameMode.Poker.Player;
using Game.Runtime.GameMode.Poker.Stages;
using Localization;
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
		[Tooltip("Items every player in the match is dealt once, at the first hand they are dealt into.")]
		[MinValue(0)]
		[SerializeField] private int _itemsPerMatch;

		[Tooltip("Items every player still in the match is dealt as a hand starts.")]
		[MinValue(0)]
		[SerializeField] private int _itemsPerHand = 2;

		[Tooltip("Extra items for everyone who did not win the previous hand, folders included.")]
		[MinValue(0)]
		[SerializeField] private int _loserBonus = 1;

		[Tooltip("Items handed to the player the winner fed a Colorful, once the eating is over, if they are still conscious.")]
		[MinValue(0)]
		[SerializeField] private int _colorfulSurvivorItems;

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

		[Tooltip("Seconds a player an item asks to choose has to answer before the table chooses for them. 0 waits for their answer however long it takes. Either way never longer than the user's turn has left, when the turn is on a clock.")]
		[MinValue(0f)]
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

		// Raised on every peer as two cards set off to change places, with how they look in flight. The swap itself
		// is written once they land.
		public event Action<PokerCardPlace, PokerCardPlace, PokerCardExchangeFlight> OnCardsExchanging;

		// An item played, on every peer: user, item, and whom it was aimed at (NoTurn for nobody). For props.
		public event Action<ulong, PokerItemType, ulong> OnItemUsed;

		// The same item done resolving, answers and flights included, on every peer: user, item. For props held
		// through the whole of it.
		public event Action<ulong, PokerItemType> OnItemResolved;

		// A card about to be rewritten in place, which faces it flickers through on the way, and what it becomes (None when it stays as it is).
		public event Action<PokerCardPlace, PokerCardFlickerFaces, CardData> OnCardRewriting;

		public PokerCardExchangePacing ExchangePacing => _exchangePacing;

		// Server only: who did not win the hand just settled, owed a bonus at the next deal.
		private readonly HashSet<ulong> _previousLosers = new();
		private readonly List<PokerItemType> _drawn = new();

		// Server only: who has had the starting items this match.
		private readonly HashSet<ulong> _startingItemsGiven = new();
		private readonly HashSet<ulong> _matchItemsGiven = new();
		private ulong _fedColorfulClientId = PokerGameData.NoTurn;

		// Server only: an item is being resolved, and the answer it is waiting on.
		private bool _resolving;
		private int _responseSlot = -1;

		public PokerItemDatabase Database => _database;
		public int Capacity => Mathf.Max(1, _capacity);
		public int ColorfulSurvivorItems => _colorfulSurvivorItems;

		public float ResponseDuration => Mathf.Max(0f, _responseDuration);
		public bool IsResponseTimed => _responseDuration > 0f;

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

		public int UsesPerStreet => Mathf.Max(1, _usesPerStreet);

		public bool HasSpentStreetUses(PokerPlayer user) => user && user.ItemInventory && user.ItemInventory.UsesThisStreet >= UsesPerStreet;

		// The one answer to "may this player play this item now", for the picker and the server alike.
		public PokerItemAvailability GetAvailability(PokerPlayer user, PokerItemType type)
		{
			if (!TryGetItem(type, out var item)) return PokerItemAvailability.Hidden(Localizer.Get(LocalizationKeys.Item.Reason.NotDealt));
			if (!user || !user.ItemInventory || !user.ItemInventory.Holds(type)) return PokerItemAvailability.Hidden(Localizer.Get(LocalizationKeys.Item.Reason.NotHeld));
			if (!GameMode || !GameMode.IsPlayingThisMatch(user.Data)) return PokerItemAvailability.Dimmed(Localizer.Get(LocalizationKeys.Item.Reason.OutOfMatch));
			if (!IsStreetCurrent) return PokerItemAvailability.Dimmed(Localizer.Get(LocalizationKeys.Item.Reason.StreetOnly));
			if (Data.CurrentTurnClientId.Value != user.ClientId) return PokerItemAvailability.Dimmed(Localizer.Get(LocalizationKeys.Item.Reason.TurnOnly));
			if (HasRule(PokerItemTableRuleKind.NoItemsSelf, StreetSerial.Value, user.ClientId)) return PokerItemAvailability.Dimmed(Localizer.Get(LocalizationKeys.Item.Reason.LockedOut));
			if (HasSpentStreetUses(user)) return PokerItemAvailability.Dimmed(UsesPerStreet == 1 ? Localizer.Get(LocalizationKeys.Item.Reason.UsedOne) : Localizer.Format(LocalizationKeys.Item.Reason.UsedMany, UsesPerStreet));
			if (PendingResponse.Value.IsPending) return PokerItemAvailability.Dimmed(Localizer.Get(LocalizationKeys.Item.Reason.WaitingOther));

			if (item.NeedsResponse && IsResponseTimed && Data.HasTurnClock && Data.TurnRemaining < ResponseDuration)
				return PokerItemAvailability.Dimmed(Localizer.Get(LocalizationKeys.Item.Reason.NoTime));

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

		public void ServerAddRule(PokerItemTableRuleKind kind, int streetSerial, int amount, ulong sourceClientId, int streets = 1)
		{
			if (!IsServer) return;

			TableRules.Add(new PokerItemTableRule
			{
				Kind = kind,
				StreetSerial = streetSerial,
				Streets = Mathf.Max(1, streets),
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
				if (rule.Kind != kind || !rule.Covers(streetSerial)) continue;
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
				if (rule.Kind == kind && rule.Covers(streetSerial)) sum += rule.Amount;
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

		// A street opening is a new street for the rules aimed at it and a fresh allowance for everybody.
		public override void OnStageStarting(PokerStage stage)
		{
			if (!IsServer || stage is not PokerStreetStage) return;

			StreetSerial.Value++;

			foreach (var player in PokerPlayer.All)
			{
				if (player && player.ItemInventory) player.ItemInventory.ServerResetStreetUses();
				if (player && player.Data) ServerHideExpiredCards(player);
			}
		}

		// Shown to the whole table for `streets` streets, starting with this one.
		public void ServerShowCard(PokerPlayer player, int slot, int streets)
		{
			if (!IsServer || !player || !player.Data) return;

			player.Data.ServerShowHoleCard(slot);
			ServerAddRule(PokerItemTableRuleKind.ShowCard, StreetSerial.Value, slot, player.ClientId, streets);
		}

		private void ServerHideExpiredCards(PokerPlayer player)
		{
			for (var slot = 0; slot < player.Data.CardCount; slot++)
			{
				if (player.Data.IsHoleCardShown(slot) && !IsCardShownOn(player.ClientId, slot, StreetSerial.Value)) player.Data.ServerHideHoleCard(slot);
			}
		}

		private bool IsCardShownOn(ulong clientId, int slot, int streetSerial)
		{
			foreach (var rule in TableRules)
			{
				if (rule.Kind == PokerItemTableRuleKind.ShowCard && rule.SourceClientId == clientId && rule.Amount == slot && rule.Covers(streetSerial)) return true;
			}

			return false;
		}

		// After the cards, before the first street: the hand is dealt, and so are the items to play it with.
		// The Colorful's eater is only rewarded once the eating beat is over, so the reward can never land
		// ahead of the roll that decides whether they are still there to take it.
		public override void OnStageEnded(PokerStage stage)
		{
			if (!IsServer) return;

			switch (stage)
			{
				case PokerDealStage:
					ServerDealItems();
					break;

				case PokerColorfulPickStage pick:
					_fedColorfulClientId = pick.FedClientId;
					break;

				case PokerBetItemConsumeStage:
					ServerRewardColorfulSurvivor();
					break;

				case PokerStreetStage:
					foreach (var player in PokerPlayer.All)
					{
						if (player && player.ItemKnowledge) player.ItemKnowledge.ServerForgetBoard();
					}

					break;
			}
		}

		private void ServerRewardColorfulSurvivor()
		{
			var clientId = _fedColorfulClientId;
			_fedColorfulClientId = PokerGameData.NoTurn;

			if (_colorfulSurvivorItems <= 0 || !_database || clientId == PokerGameData.NoTurn) return;

			var player = PokerPlayer.Find(clientId);
			if (!player || !player.Data || !player.Data.IsAlive || !player.ItemInventory) return;

			var given = ServerDealFromDatabase(player, _colorfulSurvivorItems);
			if (given > 0 && GameMode.Notices) GameMode.Notices.ServerAnnounce(PokerNotice.ForItemReward(clientId, given));
		}

		// Draws and gives, telling the player alone about whatever their full hand turned away. Returns how
		// many were kept.
		private int ServerDealFromDatabase(PokerPlayer player, int count)
		{
			if (count <= 0) return 0;

			_database.Draw(count, _drawn);

			var given = 0;
			foreach (var type in _drawn)
			{
				if (player.ItemInventory.ServerGive(type, Capacity)) given++;
			}

			var lost = _drawn.Count - given;
			if (lost > 0 && GameMode.Notices) GameMode.Notices.ServerTell(player.ClientId, PokerNotice.ForItemsLost(player.ClientId, lost));

			return given;
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
			_matchItemsGiven.Clear();
			_fedColorfulClientId = PokerGameData.NoTurn;

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
				var forMatch = _matchItemsGiven.Add(player.ClientId) ? _itemsPerMatch : 0;

				ServerDealFromDatabase(player, forMatch + _itemsPerHand + bonus);

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
				user.ItemInventory.ServerRecordUse();

				if (item.HallucinationCost > 0) user.Data.ServerChangeHallucination(item.HallucinationCost);

				var target = GameMode.FindSeatedPlayerAtSeat(request.TargetSeat);
				var targetClientId = target ? target.ClientId : PokerGameData.NoTurn;
				if (GameMode.Notices && !item.AnnouncesOutcome)
					GameMode.Notices.ServerAnnounce(PokerNotice.ForItemUsed(clientId, request.Item, targetClientId));

				PlayItemUsedRPC(clientId, request.Item, targetClientId);

				try
				{
					if (item.LeadIn > 0f) await Awaitable.WaitForSecondsAsync(item.LeadIn, ct);

					await item.UseServerAsync(context, request, ct);
				}
				finally
				{
					if (IsSpawned) PlayItemResolvedRPC(clientId, request.Item);
				}
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
		// slot they chose, or one drawn from what they could have chosen once the time runs out (never later than
		// the user's turn has left) or once they are out of the hand.
		public async Awaitable<int> ServerAskForCardAsync(PokerItemContext context, PokerItem item, PokerPlayer responder, CancellationToken ct)
		{
			if (!IsServer || !responder) return -1;

			var now = NetworkManager.ServerTime.Time;

			// A turn on a clock must always end, so an untimed answer still stops where the user's turn does.
			var duration = ResponseDuration;
			if (Data.HasTurnClock) duration = IsResponseTimed ? Mathf.Min(duration, Data.TurnRemaining) : Data.TurnRemaining;

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
				// Waiting is over when they answer, when the clock runs out, or when they are no longer in the hand
				// to answer at all, which matters most with no clock, where nothing else would end it.
				while (_responseSlot < 0 && responder && responder.Data && responder.Data.IsInHand
				       && (!PendingResponse.Value.IsTimed || NetworkManager.ServerTime.Time < PendingResponse.Value.EndTime))
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

			if (!ServerAnswerCard(GameMode.FindSeatedPlayer(sender), slot))
				Debug.LogWarning($"[{ModuleId}] Card answer from client {sender} refused: slot {slot} is not one they may put forward.");
		}

		// The one door an answer comes through, a client's RPC or a bot. False when the slot is not one the
		// responder may put forward, or nobody is asking them.
		public bool ServerAnswerCard(PokerPlayer responder, int slot)
		{
			var pending = PendingResponse.Value;
			if (!IsServer || !responder || !pending.IsPending || pending.ResponderClientId != responder.ClientId) return false;

			var requester = GameMode.FindSeatedPlayer(pending.RequesterClientId);
			if (!TryGetItem(pending.Item, out var item) || !item.AcceptsResponseCard(ContextFor(requester), responder, slot)) return false;

			_responseSlot = slot;
			return true;
		}

		// Two cards change places. Every screen is told first and flies them; the lists are written once the
		// flight is over, so no face changes in plain sight. Whatever anybody knew about either card is
		// forgotten, because the slot now holds something else. False when either card was gone by then.
		public async Awaitable<bool> ServerExchangeCardsAsync(PokerCardPlace first, PokerCardPlace second, CancellationToken ct, PokerCardExchangeFlight flight = PokerCardExchangeFlight.AsSeen)
		{
			if (!IsServer || !TryReadCard(first, out _) || !TryReadCard(second, out _)) return false;

			PlayCardExchangeRPC(first, second, flight);

			if (_exchangePacing) await Awaitable.WaitForSecondsAsync(_exchangePacing.FlightDuration, ct);

			if (!TryReadCard(first, out var firstCard) || !TryReadCard(second, out var secondCard)) return false;

			WriteCard(first, secondCard);
			WriteCard(second, firstCard);

			ServerForget(first);
			ServerForget(second);

			return true;
		}

		// One of a player's cards turned into another in place. Every screen is told first and flickers it;
		// the new face is written once the flicker is over. An invalid card leaves the old face, after the
		// same flicker, so a gamble that failed looks like one that took. Whatever anybody knew of a
		// rewritten face is forgotten.
		public async Awaitable ServerRewriteHoleCardAsync(PokerPlayer holder, int slot, CardData card, PokerCardFlickerFaces faces, CancellationToken ct)
		{
			if (!IsServer || !holder || !holder.Data || slot < 0 || slot >= holder.Data.CardCount) return;

			PlayCardRewriteRPC(PokerCardPlace.InHand(holder.ClientId, slot), faces, card);

			if (_exchangePacing) await Awaitable.WaitForSecondsAsync(_exchangePacing.RewriteDuration, ct);

			if (!card.IsValid || !holder || !holder.Data || slot >= holder.Data.CardCount) return;

			holder.Data.ServerReplaceHoleCard(slot, card);
			ServerForget(PokerCardPlace.InHand(holder.ClientId, slot));
		}

		[Rpc(SendTo.Everyone)]
		private void PlayItemUsedRPC(ulong user, PokerItemType item, ulong target) => OnItemUsed?.Invoke(user, item, target);

		[Rpc(SendTo.Everyone)]
		private void PlayItemResolvedRPC(ulong user, PokerItemType item) => OnItemResolved?.Invoke(user, item);

		[Rpc(SendTo.Everyone)]
		private void PlayCardRewriteRPC(PokerCardPlace place, PokerCardFlickerFaces faces, CardData card) => OnCardRewriting?.Invoke(place, faces, card);

		[Rpc(SendTo.Everyone)]
		private void PlayCardExchangeRPC(PokerCardPlace first, PokerCardPlace second, PokerCardExchangeFlight flight) => OnCardsExchanging?.Invoke(first, second, flight);

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
			entries.Add(new MatchConfigInt(ModuleId, LocalizationKeys.Config.Section.Items, "ItemsPerHand", LocalizationKeys.Config.ItemsPerHand, 0, 5, 1,
				() => _itemsPerHand, value => _itemsPerHand = value));
			entries.Add(new MatchConfigInt(ModuleId, LocalizationKeys.Config.Section.Items, "LoserBonus", LocalizationKeys.Config.LoserBonus, 0, 3, 1,
				() => _loserBonus, value => _loserBonus = value));
			entries.Add(new MatchConfigInt(ModuleId, LocalizationKeys.Config.Section.Items, "Capacity", LocalizationKeys.Config.ItemCapacity, 1, 10, 1,
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
