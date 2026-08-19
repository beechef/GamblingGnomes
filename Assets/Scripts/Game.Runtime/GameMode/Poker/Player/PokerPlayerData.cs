using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
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

		[HideInInspector] public NetworkVariable<int> ReportsLeft = new(0,
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

		// Separate from OnStateChanged: the wheel rebuilds its slots on this, and rebuilding a wheel every
		// time a chip moves would fight whatever the player is currently spinning.
		public event Action OnAbilitiesChanged;

		// Separate from OnStateChanged for the same reason: blood is read as a body — fingers come off with
		// it — and a hand rebuilt every time a chip moves is work on a value that did not change.
		public event Action<int, int> OnHealthChanged;

		// Money staked at the table is the same money the player owns, so there is nothing to buy in with
		// and nothing to cash out — what is bet leaves the wallet and what is won lands back in it.
		public int Chips => _wallet ? _wallet.Money.Value : 0;

		// Derived rather than stored, so it can never disagree with the health everyone can already see —
		// and so anything that ever heals a player brings them back without a second flag to remember.
		public bool IsAlive => Health.Value > 0;

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
			// that moves — it just hears it from the wallet.
			if (_wallet) _wallet.Money.OnValueChanged += HandleIntChanged;

			if (IsServer) ServerResetHealthToStart();

			SeatIndex.OnValueChanged += HandleIntChanged;
			Bet.OnValueChanged += HandleIntChanged;
			TotalBet.OnValueChanged += HandleIntChanged;
			Status.OnValueChanged += HandleStatusChanged;
			HasActed.OnValueChanged += HandleBoolChanged;
			HandRevealed.OnValueChanged += HandleBoolChanged;
			ReportsLeft.OnValueChanged += HandleIntChanged;
			Health.OnValueChanged += HandleHealthChanged;

			AbilityIds.OnListChanged += HandleAbilitiesChanged;
			HoleCards.OnListChanged += HandleHoleCardsChanged;

			OnHandVisibilityRulesChanged += HandleStateChanged;
		}

		public override void OnNetworkDespawn()
		{
			if (_wallet) _wallet.Money.OnValueChanged -= HandleIntChanged;

			SeatIndex.OnValueChanged -= HandleIntChanged;
			Bet.OnValueChanged -= HandleIntChanged;
			TotalBet.OnValueChanged -= HandleIntChanged;
			Status.OnValueChanged -= HandleStatusChanged;
			HasActed.OnValueChanged -= HandleBoolChanged;
			HandRevealed.OnValueChanged -= HandleBoolChanged;
			ReportsLeft.OnValueChanged -= HandleIntChanged;
			Health.OnValueChanged -= HandleHealthChanged;

			AbilityIds.OnListChanged -= HandleAbilitiesChanged;
			HoleCards.OnListChanged -= HandleHoleCardsChanged;

			OnHandVisibilityRulesChanged -= HandleStateChanged;
		}

		public void ServerTakeSeat(int seatIndex)
		{
			if (!IsServer) return;

			SeatIndex.Value = seatIndex;
			Status.Value = PokerPlayerStatus.Waiting;
			ServerResetForHand();
		}

		public void ServerLeaveSeat()
		{
			if (!IsServer) return;

			SeatIndex.Value = NoSeat;
			Status.Value = PokerPlayerStatus.Waiting;
			ServerResetForHand();
		}

		public void ServerResetForHand()
		{
			if (!IsServer) return;

			Bet.Value = 0;
			TotalBet.Value = 0;
			HasActed.Value = false;
			HandRevealed.Value = false;
			ReportsLeft.Value = 0;
			AbilityIds.Clear();
			HoleCards.Clear();
		}

		// A new match rather than a new hand. Blood and money are what a match is played *with* — they
		// carry from hand to hand and losing the last of either is how a player stops being dealt in — so
		// this is the one place they go back, and it belongs to the table going idle rather than to any
		// hand ending.
		public void ServerResetForMatch()
		{
			if (!IsServer) return;

			ServerResetForHand();

			ServerResetHealthToStart();
			Status.Value = PokerPlayerStatus.Waiting;

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

		public void ServerRevealHand()
		{
			if (!IsServer) return;

			HandRevealed.Value = true;
		}

		public int ServerPlaceBet(int amount)
		{
			if (!IsServer) return 0;

			var paid = Mathf.Clamp(amount, 0, Chips);
			if (paid > 0 && !_wallet.ServerTryWithdraw(paid)) return 0;

			Bet.Value += paid;
			TotalBet.Value += paid;

			if (Chips <= 0) Status.Value = PokerPlayerStatus.AllIn;

			return paid;
		}

		// Money that leaves a player without ever becoming a bet. It answers nothing on the street, so it
		// must not land in front of them where a call would read it as already paid — the caller is the
		// one that puts it in the pot.
		public int ServerPayIntoPot(int amount)
		{
			if (!IsServer) return 0;

			var paid = Mathf.Clamp(amount, 0, Chips);
			if (paid > 0 && !_wallet.ServerTryWithdraw(paid)) return 0;

			if (Chips <= 0) Status.Value = PokerPlayerStatus.AllIn;

			return paid;
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

		private void HandleHealthChanged(int previous, int current)
		{
			OnHealthChanged?.Invoke(previous, current);
			OnStateChanged?.Invoke();
		}
		private void HandleAbilitiesChanged(NetworkListEvent<FixedString64Bytes> changeEvent) => OnAbilitiesChanged?.Invoke();
		private void HandleBoolChanged(bool previous, bool current) => OnStateChanged?.Invoke();
		private void HandleStatusChanged(PokerPlayerStatus previous, PokerPlayerStatus current) => OnStateChanged?.Invoke();
		private void HandleHoleCardsChanged(NetworkListEvent<CardData> changeEvent) => OnHoleCardsChanged?.Invoke(changeEvent);
	}
}