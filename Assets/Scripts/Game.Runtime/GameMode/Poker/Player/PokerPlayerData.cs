using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
using Game.Runtime.GameMode.Poker.Items;
using Game.Runtime.Player;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Player
{
	// Everything the table knows about one player. Hole cards sit here too, on a list only their owner
	// is allowed to read — the network layer does the hiding, so no code path can leak a hand by
	// accident the way a shared list plus manual filtering could.
	public class PokerPlayerData : NetworkBehaviour
	{
		public const int NoSeat = -1;

		// The ceiling is the scale: the bar reads as a percentage, so it is a constant rather than
		// something to tune, and every gain is a number of points out of this.
		public const int MaxHallucination = 100;

		[Header("References")]
		[Tooltip("What the player bets with. There is no separate stack in front of them — the wallet is the stack.")]
		[SerializeField] private PlayerData _wallet;

		[Header("Blood")]
		[Tooltip("Blood a player sits down with, and gets back when a new match starts.")]
		[MinValue(1)]
		[SerializeField] private int _startingHealth = 10;

		[Tooltip("Ceiling anything healing them can reach. Separate from the starting amount so a match can be begun below full and still be worth healing.")]
		[MinValue(1)]
		[SerializeField] private int _maxHealth = 10;

		[HideInInspector] public NetworkVariable<int> SeatIndex = new(NoSeat,
			readPerm: NetworkVariableReadPermission.Everyone,
			writePerm: NetworkVariableWritePermission.Server);

		[HideInInspector] public NetworkVariable<int> Bet = new(0,
			readPerm: NetworkVariableReadPermission.Everyone,
			writePerm: NetworkVariableWritePermission.Server);

		[HideInInspector] public NetworkVariable<int> TotalBet = new(0,
			readPerm: NetworkVariableReadPermission.Everyone,
			writePerm: NetworkVariableWritePermission.Server);

		[HideInInspector] public NetworkVariable<PokerPlayerStatus> Status = new(PokerPlayerStatus.Waiting,
			readPerm: NetworkVariableReadPermission.Everyone,
			writePerm: NetworkVariableWritePermission.Server);

		[HideInInspector] public NetworkVariable<bool> HasActed = new(false,
			readPerm: NetworkVariableReadPermission.Everyone,
			writePerm: NetworkVariableWritePermission.Server);

		// Whether this body was collected into the match that is running. Stamped when the match begins and
		// false for anybody who sat down after — a chair arriving mid-match is a seat in the room, not a
		// place in the game, so they wager nothing, are dealt nothing and cannot be fed. Replicated because
		// every view drawing them has to know which of the two they are.
		//
		// Its own value rather than read off Status: a mid-match arrival is Waiting, and so is everybody
		// else between two hands. Nothing already on the wire separates "not playing yet" from "not playing
		// at all", which is exactly the distinction this exists for.
		[HideInInspector] public NetworkVariable<bool> InMatch = new(false,
			readPerm: NetworkVariableReadPermission.Everyone,
			writePerm: NetworkVariableWritePermission.Server);

		// Showdown, or anything else that decides this hand is public.
		[HideInInspector] public NetworkVariable<bool> HandRevealed = new(false,
			readPerm: NetworkVariableReadPermission.Everyone,
			writePerm: NetworkVariableWritePermission.Server);

		// The ability game's per-player hand. Owner-read: which tricks somebody drew is the whole guessing
		// game, so the network layer keeps them from everyone else. A list rather than a single slot because
		// the wheel deals several and the player picks; spending one removes it, which is why there is no
		// separate "used" flag — what is left in the list is what is left to play. Riding replicated state
		// rather than a fired-off RPC means a client that spawns late still arrives knowing its own hand.
		public readonly NetworkList<FixedString64Bytes> AbilityIds = new(null,
			NetworkVariableReadPermission.Owner,
			NetworkVariableWritePermission.Server);

		// Server time this player is free to play another card. Owner-read for the same reason the hand is:
		// "somebody is busy" would say that somebody just played something, and the whole guessing game is
		// that only the act itself ever tells the table anything. Absolute rather than a countdown, so a
		// client arriving mid-trick reads how long is left instead of starting the clock again.
		[HideInInspector] public NetworkVariable<double> AbilityBusyUntil = new(0d,
			readPerm: NetworkVariableReadPermission.Owner,
			writePerm: NetworkVariableWritePermission.Server);

		[HideInInspector] public NetworkVariable<int> ReportsLeft = new(0,
			readPerm: NetworkVariableReadPermission.Everyone,
			writePerm: NetworkVariableWritePermission.Server);

		// The wallet, itemised: one type per unit of money, in the order they will be staked — a bet
		// takes from the front, so which mushrooms it spends is the wallet's order and never a choice,
		// and a player can read off the front what their next call will put on the table. Mirrors
		// PlayerData.Money the way PotItems mirrors the pot: the scalar stays what every rule computes
		// with, and the server re-syncs this list on every change of it, whoever moved the money.
		public readonly NetworkList<PokerItemUnit> StakeItems = new(null,
			NetworkVariableReadPermission.Everyone,
			NetworkVariableWritePermission.Server);
		// their heads. Only ever rises, except where an item brings it down.
		[HideInInspector] public NetworkVariable<int> HallucinationRate = new(0,
			readPerm: NetworkVariableReadPermission.Everyone,
			writePerm: NetworkVariableWritePermission.Server);

		// Which of this hand's slots their holder has picked up. Read by **everyone**, because lifting a
		// card off the table is an act the whole table watches — the same split the cheat abilities make:
		// the act is public, what it tells you is not. The faces stay hidden by IsHoleCardVisible, which
		// asks IsOwner; this only says which cards are in a hand rather than lying face down.
		[HideInInspector] public NetworkVariable<int> LookedAtHoleCards = new(0,
			readPerm: NetworkVariableReadPermission.Everyone,
			writePerm: NetworkVariableWritePermission.Server);

		// How many of their own cards a player is allowed to turn over. Zero or less is a hand held the
		// ordinary way, where the holder sees all of it — which is what Hold'em wants and what this
		// reduces to when nothing sets it.
		[HideInInspector] public NetworkVariable<int> ViewableHoleCards = new(0,
			readPerm: NetworkVariableReadPermission.Everyone,
			writePerm: NetworkVariableWritePermission.Server);

		// Read by everyone the way the wireframe shows it over a head: how hurt somebody is, is table
		// information. Nothing damages it yet — abilities will, through ServerChangeHealth, so every
		// future source of harm goes through the same clamp.
		[HideInInspector] public NetworkVariable<int> Health = new(0,
			readPerm: NetworkVariableReadPermission.Everyone,
			writePerm: NetworkVariableWritePermission.Server);

		// Replicated to everyone rather than owner-only: who may look is a display rule, so an ability
		// that shows someone else's hand is a change of rule and not a change of plumbing. The trade is
		// that a modified client can read the list, so the rule is the only thing hiding it.
		public readonly NetworkList<CardData> HoleCards = new(null,
			NetworkVariableReadPermission.Everyone,
			NetworkVariableWritePermission.Server);

		// Installed by whatever grants extra sight — a cheat ability, a spectator mode, a debug view.
		// A list rather than a single slot so two of them can coexist; any one saying yes is enough.
		private static readonly List<Func<PokerPlayerData, bool>> HandVisibilityProviders = new();

		public static void AddHandVisibilityProvider(Func<PokerPlayerData, bool> provider)
		{
			if (provider != null && !HandVisibilityProviders.Contains(provider)) HandVisibilityProviders.Add(provider);
		}

		public static void RemoveHandVisibilityProvider(Func<PokerPlayerData, bool> provider)
		{
			HandVisibilityProviders.Remove(provider);
		}

		// Raised by a provider whose answer has just changed. The hand it now covers has no way of noticing
		// that on its own — nothing about that hand changed, only who is allowed to look at it.
		public static event Action OnHandVisibilityRulesChanged;

		public static void NotifyHandVisibilityRulesChanged() => OnHandVisibilityRulesChanged?.Invoke();

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void ResetStatics()
		{
			HandVisibilityProviders.Clear();
			OnHandVisibilityRulesChanged = null;
		}

		public event Action OnStateChanged;

		// Carries the change so a hand can deal one card in without disturbing the others.
		public event Action<NetworkListEvent<CardData>> OnHoleCardsChanged;

		// How the hole cards read right now, as opposed to which cards they are: which of them this client
		// may look at, and which are up in the hand rather than lying on the table. Separate from
		// OnStateChanged because everything drawing a hand was waking on every chip that moved and then
		// asking whether anything about the cards had changed — the answer was almost always no, and a view
		// that has to check whether it was called for a reason it cares about is a view whose subscription
		// says nothing about what it does. Raised by LookedAtHoleCards, HandRevealed and the visibility
		// rules, which are the three things IsHoleCardVisible and IsHoleCardInHand are built from.
		public event Action OnHoleCardPresentationChanged;

		// Separate from OnStateChanged: the wheel rebuilds its slots on this, and rebuilding a wheel every
		// time a chip moves would fight whatever the player is currently spinning.
		public event Action OnAbilitiesChanged;

		// Separate from OnStateChanged for the same reason: blood is read as a body — fingers come off with
		// it — and a hand rebuilt every time a chip moves is work on a value that did not change.
		public event Action<int, int> OnHealthChanged;

		// Separate from OnStateChanged for the same reason blood is: the effects a rate crosses into are
		// picked on the change itself, and rebuilding them every time a chip moves would re-roll a
		// hallucination nobody caused.
		public event Action<int, int> OnHallucinationChanged;

		// The wallet's itemised half changed — a unit drawn in, or spent off the front.
		public event Action OnStakeItemsChanged;

		// Stamped by the mode, the same way the configured starting stats are. Empty draws plain chips.
		private PokerItemDatabase _stakeItemSource;

		// Types of the units currently standing in front of the player as Bet, in the order they were
		// staked. Server-only scratch: the pot ledger is what replicates, and it takes these at collect.
		private readonly List<PokerItemType> _committedItemTypes = new();

		private readonly List<PokerItemType> _drawBuffer = new();

		// Money staked at the table is the same money the player owns, so there is nothing to buy in with
		// and nothing to cash out — what is bet leaves the wallet and what is won lands back in it.
		public int Chips => _wallet ? _wallet.Money.Value : 0;

		// Derived rather than stored, so it can never disagree with the number everyone can already see —
		// and so anything that ever sobers a player brings them back without a second flag to remember.
		// Hallucination is the only way out of this game: blood is still tracked and still drawn on the
		// body, but running out of it is not what ends a player here.
		public bool IsAlive => HallucinationRate.Value < MaxHallucination;

		public int StartingHealth => Mathf.Clamp(_startingHealthOverride > 0 ? _startingHealthOverride : _startingHealth, 1, MaxHealth);

		public int MaxHealth => Mathf.Max(1, _maxHealth);

		// Whether the mode has stamped its configured stake onto this body yet. Server-only, like the
		// override itself — the latch is what lets a late joiner be reset exactly once.
		public bool HasConfiguredStartingStats { get; private set; }

		private int _startingHealthOverride = -1;

		public void ServerSetStartingStats(int money, int health)
		{
			if (!IsServer) return;

			if (_wallet) _wallet.ServerSetStartingMoney(money);
			_startingHealthOverride = Mathf.Max(1, health);
			HasConfiguredStartingStats = true;
		}

		public bool IsSeated => SeatIndex.Value != NoSeat;
		public bool IsInHand => Status.Value is PokerPlayerStatus.Active or PokerPlayerStatus.AllIn;
		public bool CanAct => Status.Value == PokerPlayerStatus.Active;
		public int CardCount => HoleCards.Count;

		public bool IsHandVisible => IsOwner || HandRevealed.Value || IsHandVisibleToProvider();

		// The same question asked of one card. A hand held face down to its own holder — five dealt, three
		// they may turn — is the only case where these two answers differ, and they differ *for the owner*:
		// everyone else is told exactly what IsHandVisible tells them. Views ask this one, so a table that
		// never limits the looking behaves as it always did.
		public bool IsHoleCardVisible(int slot)
		{
			if (HandRevealed.Value || IsHandVisibleToProvider()) return true;
			if (!IsOwner) return false;

			return !HasLookLimit || HasLookedAt(slot);
		}

		public bool HasLookLimit => ViewableHoleCards.Value > 0;

		public bool HasLookedAt(int slot) => slot >= 0 && slot < 31 && (LookedAtHoleCards.Value & (1 << slot)) != 0;

		// Where this card physically is. A card lifted off the table is in its holder's hand until the
		// hand is shown, at which point everything goes back down for the table to read — which is why
		// this is derived rather than replicated: the two facts it needs are already on the wire.
		public bool IsHoleCardInHand(int slot) => !HandRevealed.Value && HasLookedAt(slot);

		public int LookedAtCount
		{
			get
			{
				var count = 0;
				for (var slot = 0; slot < HoleCards.Count && slot < 31; slot++)
				{
					if (HasLookedAt(slot)) count++;
				}

				return count;
			}
		}

		// Whether this player may still turn one over. Read by the server before it grants a look and by
		// the view that draws the cards, so the two cannot offer different answers.
		public bool CanLookAt(int slot)
		{
			if (!HasLookLimit) return false;
			if (slot < 0 || slot >= HoleCards.Count) return false;
			if (HasLookedAt(slot)) return false;

			return LookedAtCount < ViewableHoleCards.Value;
		}

		// Sight somebody was granted, as opposed to a hand that is simply public. A showdown turns every hand
		// face up for everyone; this is only true where an ability handed this client a look it was not owed,
		// which is the difference anything drawing "what I have been shown" has to be able to see.
		public bool IsHandVisibleByGrant => !IsOwner && !HandRevealed.Value && IsHandVisibleToProvider();

		private bool IsHandVisibleToProvider()
		{
			foreach (var provider in HandVisibilityProviders)
			{
				if (provider != null && provider.Invoke(this)) return true;
			}

			return false;
		}

		public override void OnNetworkSpawn()
		{
			if (!_wallet) _wallet = GetComponent<PlayerData>();

			// The stack is the wallet now, so a view watching this player still hears about every chip
			// that moves — it just hears it from the wallet. The same change is what keeps the itemised
			// wallet in step: money arriving draws its types there and then.
			if (_wallet) _wallet.Money.OnValueChanged += HandleMoneyChanged;

			if (IsServer)
			{
				ServerResetHealthToStart();
				ServerSyncStakeItems();
			}

			SeatIndex.OnValueChanged += HandleIntChanged;
			Bet.OnValueChanged += HandleIntChanged;
			TotalBet.OnValueChanged += HandleIntChanged;
			Status.OnValueChanged += HandleStatusChanged;
			HasActed.OnValueChanged += HandleBoolChanged;
			HandRevealed.OnValueChanged += HandleHandRevealedChanged;
			ReportsLeft.OnValueChanged += HandleIntChanged;
			Health.OnValueChanged += HandleHealthChanged;
			LookedAtHoleCards.OnValueChanged += HandleLookedAtChanged;
			HallucinationRate.OnValueChanged += HandleHallucinationChanged;

			AbilityIds.OnListChanged += HandleAbilitiesChanged;
			HoleCards.OnListChanged += HandleHoleCardsChanged;
			StakeItems.OnListChanged += HandleStakeItemsChanged;

			OnHandVisibilityRulesChanged += HandleVisibilityRulesChanged;
		}

		public override void OnNetworkDespawn()
		{
			if (_wallet) _wallet.Money.OnValueChanged -= HandleMoneyChanged;

			SeatIndex.OnValueChanged -= HandleIntChanged;
			Bet.OnValueChanged -= HandleIntChanged;
			TotalBet.OnValueChanged -= HandleIntChanged;
			Status.OnValueChanged -= HandleStatusChanged;
			HasActed.OnValueChanged -= HandleBoolChanged;
			HandRevealed.OnValueChanged -= HandleHandRevealedChanged;
			ReportsLeft.OnValueChanged -= HandleIntChanged;
			Health.OnValueChanged -= HandleHealthChanged;
			HallucinationRate.OnValueChanged -= HandleHallucinationChanged;
			LookedAtHoleCards.OnValueChanged -= HandleLookedAtChanged;

			AbilityIds.OnListChanged -= HandleAbilitiesChanged;
			HoleCards.OnListChanged -= HandleHoleCardsChanged;
			StakeItems.OnListChanged -= HandleStakeItemsChanged;

			OnHandVisibilityRulesChanged -= HandleVisibilityRulesChanged;
		}

		// Sitting down is a chair, never a place in the match already running. StartGame is the one thing
		// that stamps somebody in, so a body arriving after it stays out until the next one begins.
		public void ServerTakeSeat(int seatIndex)
		{
			if (!IsServer) return;

			SeatIndex.Value = seatIndex;
			Status.Value = PokerPlayerStatus.Waiting;
			InMatch.Value = false;
			ServerResetForHand();
		}

		public void ServerLeaveSeat()
		{
			if (!IsServer) return;

			SeatIndex.Value = NoSeat;
			Status.Value = PokerPlayerStatus.Waiting;
			InMatch.Value = false;
			ServerResetForHand();
		}

		// Mucking, as opposed to a swap: a clear is exactly what putting the cards down looks like on the
		// wire, and PokerHandVisual plays its tear-down off the Clear event. ServerReplaceHoleCards writes
		// slots instead for the opposite reason — a cheat must not read as a deal.
		public void ServerFold()
		{
			if (!IsServer || !IsInHand) return;

			Status.Value = PokerPlayerStatus.Folded;
			HasActed.Value = true;
			HoleCards.Clear();
		}

		public void ServerResetForHand()
		{
			if (!IsServer) return;

			Bet.Value = 0;
			TotalBet.Value = 0;
			_committedItemTypes.Clear();
			HasActed.Value = false;
			HandRevealed.Value = false;
			ReportsLeft.Value = 0;
			HoleCards.Clear();

			// What a player is carrying is PokerAbilityModule's, not this class's: a table whose items are
			// meant to be stockpiled across hands had its inventory swept here regardless, which made that
			// module's own setting a lie. The match reset below still empties it, because a stockpile
			// belongs to the match it was built up in.

			// A new hand is fresh cards nobody has dared to look at yet.
			LookedAtHoleCards.Value = 0;
		}

		// A new match rather than a new hand. Blood and money are what a match is played *with* — they
		// carry from hand to hand and losing the last of either is how a player stops being dealt in — so
		// this is the one place they go back, and it belongs to the table going idle rather than to any
		// hand ending.
		public void ServerResetForMatch()
		{
			if (!IsServer) return;

			ServerResetForHand();

			AbilityIds.Clear();
			AbilityBusyUntil.Value = 0d;

			ServerResetHealthToStart();
			HallucinationRate.Value = 0;
			Status.Value = PokerPlayerStatus.Waiting;
			InMatch.Value = false;

			// The wallet resets itself. What a purse starts with is its own business, and reaching in to
			// set it from here would be a second place to keep in step with the first.
			if (_wallet) _wallet.ServerResetToStart();
		}

		public void ServerResetHealthToStart()
		{
			if (!IsServer) return;

			Health.Value = StartingHealth;
		}

		public void ServerResetForRound()
		{
			if (!IsServer) return;

			Bet.Value = 0;
			HasActed.Value = false;
			_committedItemTypes.Clear();
		}

		// A hand swapped where it lies, as opposed to dealt. Clearing the list and refilling it is what a
		// deal looks like, and it looks like one to every table: the visuals tear their cards down and flip
		// the replacements in one at a time. That is a tell, and the one card in the game whose whole worth
		// is that nobody noticed cannot afford it. Writing each slot raises a Value change instead, which a
		// hand nobody may see reads as no change at all — the card was a face-down back before and is a
		// face-down back after, so nothing moves on any screen but the owner's.
		public void ServerReplaceHoleCards(IReadOnlyList<CardData> cards)
		{
			if (!IsServer) return;

			var shared = Mathf.Min(cards.Count, HoleCards.Count);
			for (var i = 0; i < shared; i++) HoleCards[i] = cards[i];

			// Only reached by a caller swapping a different number of cards than are being held, which a
			// redraw never does — kept so this cannot silently drop or keep one.
			for (var i = HoleCards.Count - 1; i >= cards.Count; i--) HoleCards.RemoveAt(i);
			for (var i = shared; i < cards.Count; i++) HoleCards.Add(cards[i]);
		}

		public void ServerSetHoleCards(IReadOnlyList<CardData> cards)
		{
			if (!IsServer) return;

			HoleCards.Clear();
			foreach (var card in cards) HoleCards.Add(card);
		}

		// Turning one of your own cards over. Asked of the server rather than decided locally: the limit is
		// a rule, and a client that lied about it would be reading a card the round says it may not.
		[Rpc(SendTo.Server)]
		public void LookAtHoleCardRPC(int slot)
		{
			if (!CanLookAt(slot)) return;

			LookedAtHoleCards.Value |= 1 << slot;

			// Reaching for a card is something the table watches, so the gesture is played on the server for
			// everyone rather than locally by whoever pressed. A state the art has not landed yet is skipped
			// silently by PlayerActionAnimator, so this can be wired before there is anything to play.
			GetComponent<PokerPlayer>()?.ActionAnimator?.ServerPlay(PlayerActionIds.PickUpCard);
		}

		// The mode stamps this beside the starting stats: how many of the five its round lets a player see.
		public void ServerSetViewableHoleCards(int count)
		{
			if (!IsServer) return;

			ViewableHoleCards.Value = Mathf.Max(0, count);
		}

		public void ServerRevealHand()
		{
			if (!IsServer) return;

			HandRevealed.Value = true;
		}

		public int ServerPlaceBet(int amount)
		{
			if (!IsServer) return 0;

			var paid = Mathf.Clamp(amount, 0, Chips);
			if (paid > 0)
			{
				if (!ServerDrawAndWithdraw(paid)) return 0;

				_committedItemTypes.AddRange(_drawBuffer);
			}

			Bet.Value += paid;
			TotalBet.Value += paid;

			if (Chips <= 0) Status.Value = PokerPlayerStatus.AllIn;

			return paid;
		}

		// Money that leaves a player without ever becoming a bet. It answers nothing on the street, so it
		// must not land in front of them where a call would read it as already paid — the caller is the
		// one that puts it in the pot, and the types drawn off the wallet go with it.
		public int ServerPayIntoPot(int amount, List<PokerItemType> drawnTypes = null)
		{
			if (!IsServer) return 0;

			var paid = Mathf.Clamp(amount, 0, Chips);
			if (paid > 0)
			{
				if (!ServerDrawAndWithdraw(paid)) return 0;

				drawnTypes?.AddRange(_drawBuffer);
			}

			if (Chips <= 0) Status.Value = PokerPlayerStatus.AllIn;

			return paid;
		}

		// The units leave the front of the wallet in the same act as the money, so which mushrooms a
		// stake spends is the wallet's order and never a choice. A refused withdrawal puts them back
		// where they were — the draw and the money move together or not at all.
		private bool ServerDrawAndWithdraw(int amount)
		{
			_drawBuffer.Clear();

			for (var i = 0; i < amount; i++)
			{
				if (StakeItems.Count > 0)
				{
					_drawBuffer.Add(StakeItems[0]);
					StakeItems.RemoveAt(0);
				}
				else
				{
					_drawBuffer.Add(PokerItemDatabase.PlainChip);
				}
			}

			if (_wallet.ServerTryWithdraw(amount)) return true;

			for (var i = _drawBuffer.Count - 1; i >= 0; i--) StakeItems.Insert(0, _drawBuffer[i]);
			return false;
		}

		// The types standing in front of this player as Bet, handed over as the money is collected. The
		// scalar is the authority: a count the committed list cannot cover is padded with plain chips
		// rather than dropped, so the pot ledger never loses a unit to a path that bypassed PlaceBet.
		public void ServerTakeCommittedItemTypes(int expected, List<PokerItemType> into)
		{
			into.Clear();
			if (!IsServer) return;

			for (var i = 0; i < expected; i++)
			{
				into.Add(i < _committedItemTypes.Count ? _committedItemTypes[i] : PokerItemDatabase.PlainChip);
			}

			_committedItemTypes.Clear();
		}

		// A new source re-types the whole wallet: the body spawned and self-seeded plain chips before the
		// mode could say what a unit is here, and topping up around those would leave the starting stake
		// untyped forever. Guarded on an actual change, because the mode re-stamps on every roster
		// refresh and a re-deal mid-match would shuffle what a player already knows they are holding.
		public void ServerSetStakeItemSource(PokerItemDatabase database)
		{
			if (!IsServer || _stakeItemSource == database) return;

			_stakeItemSource = database;
			StakeItems.Clear();
			ServerSyncStakeItems();
		}

		// Money arriving from anywhere — a won pot, a house rule's sale, a match reset — is given its
		// types here, which is what "assigned as it enters the wallet" means. Trims from the back on
		// the way down, because the front is the spending order and only a real stake may take it.
		public void ServerSyncStakeItems()
		{
			if (!IsServer) return;

			while (StakeItems.Count < Chips)
			{
				StakeItems.Add(_stakeItemSource ? _stakeItemSource.DrawItemType() : PokerItemDatabase.PlainChip);
			}

			while (StakeItems.Count > Chips) StakeItems.RemoveAt(StakeItems.Count - 1);
		}

		public void ServerWinChips(int amount) => ServerGainChips(amount);

		// Money arriving from anywhere at all. Kept beside ServerWinChips rather than folded into it,
		// because a pot being paid out and a house rule selling somebody a stake are the same transfer and
		// two different events — and a call site that reads "won" for the second one is a lie the next
		// person to read it has to untangle.
		public void ServerGainChips(int amount)
		{
			if (!IsServer || !_wallet) return;

			_wallet.ServerDeposit(amount);
		}

		// Negative sobers, positive sends them further under; the clamp lives here for the same reason
		// the health one does. Passing the ceiling is death, and IsAlive reads that off this number.
		public void ServerChangeHallucination(int delta)
		{
			if (!IsServer) return;

			HallucinationRate.Value = Mathf.Clamp(HallucinationRate.Value + delta, 0, MaxHallucination);
		}

		// Negative hurts, positive heals; the clamp lives here so no source of harm can overshoot it.
		public void ServerChangeHealth(int delta)
		{
			if (!IsServer) return;

			Health.Value = Mathf.Clamp(Health.Value + delta, 0, MaxHealth);
		}

		public void ServerCollectBet()
		{
			if (!IsServer) return;

			Bet.Value = 0;
		}

		private void HandleStateChanged() => OnStateChanged?.Invoke();
		private void HandleIntChanged(int previous, int current) => OnStateChanged?.Invoke();

		private void HandleMoneyChanged(int previous, int current)
		{
			if (IsServer) ServerSyncStakeItems();

			OnStateChanged?.Invoke();
		}

		private void HandleStakeItemsChanged(NetworkListEvent<PokerItemUnit> changeEvent) => OnStakeItemsChanged?.Invoke();

		private void HandleHealthChanged(int previous, int current)
		{
			OnHealthChanged?.Invoke(previous, current);
			OnStateChanged?.Invoke();
		}
		// Turning a card over changes no card — only who may look at one — so this is the visibility rule
		// changing rather than the hand. OnStateChanged carries it to the views that redraw on it.
		// Purely about how the hand reads — nothing that watches a player's money or turn has any use for
		// it — so it goes out on the presentation event alone.
		private void HandleLookedAtChanged(int previous, int current) => OnHoleCardPresentationChanged?.Invoke();

		// Both: turning the hand over is how the cards read *and* a fact about the hand being over, which
		// is player state like any other.
		private void HandleHandRevealedChanged(bool previous, bool current)
		{
			OnHoleCardPresentationChanged?.Invoke();
			OnStateChanged?.Invoke();
		}

		private void HandleVisibilityRulesChanged() => OnHoleCardPresentationChanged?.Invoke();

		private void HandleHallucinationChanged(int previous, int current)
		{
			OnHallucinationChanged?.Invoke(previous, current);
			OnStateChanged?.Invoke();
		}

		private void HandleAbilitiesChanged(NetworkListEvent<FixedString64Bytes> changeEvent) => OnAbilitiesChanged?.Invoke();
		private void HandleBoolChanged(bool previous, bool current) => OnStateChanged?.Invoke();
		private void HandleStatusChanged(PokerPlayerStatus previous, PokerPlayerStatus current) => OnStateChanged?.Invoke();
		private void HandleHoleCardsChanged(NetworkListEvent<CardData> changeEvent) => OnHoleCardsChanged?.Invoke(changeEvent);
	}
}