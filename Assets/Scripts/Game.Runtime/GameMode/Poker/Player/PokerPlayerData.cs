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

		// The ceiling is the scale: the bar reads as a percentage, so it is a constant rather than
		// something to tune, and every gain is a number of points out of this.
		public const int MaxHallucination = 100;

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

		[HideInInspector] public NetworkVariable<PokerPlayerStatus> Status = new(PokerPlayerStatus.Waiting,
			readPerm: NetworkVariableReadPermission.Everyone,
			writePerm: NetworkVariableWritePermission.Server);

		[HideInInspector] public NetworkVariable<bool> HasActed = new(false,
			readPerm: NetworkVariableReadPermission.Everyone,
			writePerm: NetworkVariableWritePermission.Server);

		// Whether this body was collected into the match that is running. Stamped when the match begins and
		// false for anybody who sat down after — a chair arriving mid-match is a seat in the room, not a
		// place in the game, so they bet nothing, are dealt nothing and cannot be fed. Replicated because
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

		// How far under the mushrooms have taken them, out of MaxHallucination. Public, because who is closest
		// to going under is what the round's two real decisions are about. Only ever rises, except where an
		// item brings it down.
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

		// Separate from OnStateChanged for the same reason: blood is read as a body — fingers come off with
		// it — and a hand rebuilt every time a chip moves is work on a value that did not change.
		public event Action<int, int> OnHealthChanged;

		// Separate from OnStateChanged for the same reason blood is: the effects a rate crosses into are
		// picked on the change itself, and rebuilding them every time a chip moves would re-roll a
		// hallucination nobody caused.
		public event Action<int, int> OnHallucinationChanged;

		// Derived rather than stored, so it can never disagree with the number everyone can already see —
		// and so anything that ever sobers a player brings them back without a second flag to remember.
		// Hallucination is the only way out of this game: blood is still tracked and still drawn on the
		// body, but running out of it is not what ends a player here.
		public bool IsAlive => HallucinationRate.Value < MaxHallucination;

		public int StartingHealth => Mathf.Clamp(_startingHealthOverride > 0 ? _startingHealthOverride : _startingHealth, 1, MaxHealth);

		public int MaxHealth => Mathf.Max(1, _maxHealth);

		// Whether the mode has stamped its configured stats onto this body yet. Server-only, like the
		// override itself — the latch is what lets a late joiner be reset exactly once.
		public bool HasConfiguredStartingStats { get; private set; }

		private int _startingHealthOverride = -1;

		public void ServerSetStartingHealth(int health)
		{
			if (!IsServer) return;

			_startingHealthOverride = Mathf.Max(1, health);
			HasConfiguredStartingStats = true;
		}

		public bool IsSeated => SeatIndex.Value != NoSeat;
		public bool IsInHand => Status.Value == PokerPlayerStatus.Active;
		public bool CanAct => Status.Value == PokerPlayerStatus.Active;
		public int CardCount => HoleCards.Count;

		public bool IsHandVisible => IsOwner || HandRevealed.Value || IsHandVisibleToProvider();

		// The same question asked of one card. A hand held face down to its own holder — five dealt, three
		// they may turn — is the only case where these two answers differ, and they differ *for the owner*:
		// everyone else is told exactly what IsHandVisible tells them. Views ask this one, so a table that
		// never limits the looking behaves as it always did.
		public bool IsHoleCardVisible(int slot)
		{
			// A mucked hand stays on the table face down for everyone, its holder included.
			if (IsFolded) return false;
			if (HandRevealed.Value || IsHandVisibleToProvider()) return true;
			if (!IsOwner) return false;

			return !HasLookLimit || HasLookedAt(slot);
		}

		public bool HasLookLimit => ViewableHoleCards.Value > 0;

		public bool HasLookedAt(int slot) => slot >= 0 && slot < 31 && (LookedAtHoleCards.Value & (1 << slot)) != 0;

		// Where this card physically is. A card lifted off the table is in its holder's hand until the
		// hand is shown, at which point everything goes back down for the table to read — which is why
		// this is derived rather than replicated: the two facts it needs are already on the wire.
		public bool IsHoleCardInHand(int slot) => !IsFolded && !HandRevealed.Value && HasLookedAt(slot);

		// Whether any card is up in the hand rather than lying on the table: the pose the body is in, which
		// anything done with the hands has to be performed around.
		public bool IsHoldingCards
		{
			get
			{
				for (var slot = 0; slot < CardCount; slot++)
				{
					if (IsHoleCardInHand(slot)) return true;
				}

				return false;
			}
		}

		// Folding puts the cards down rather than taking them away: they lie in front of the folder until the
		// next deal clears them, so the table watches a hand being thrown in instead of one vanishing.
		public bool IsFolded => Status.Value == PokerPlayerStatus.Folded;

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
			if (!HasLookLimit || IsFolded) return false;
			if (slot < 0 || slot >= HoleCards.Count) return false;
			if (HasLookedAt(slot)) return false;

			return LookedAtCount < ViewableHoleCards.Value;
		}

		// Sight somebody was granted, as opposed to a hand that is simply public. A showdown turns every hand
		// face up for everyone; this is only true where an ability handed this client a look it was not owed,
		// which is the difference anything drawing "what I have been shown" has to be able to see.
		public bool IsHandVisibleByGrant => !IsOwner && !IsFolded && !HandRevealed.Value && IsHandVisibleToProvider();

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
			if (IsServer) ServerResetHealthToStart();

			SeatIndex.OnValueChanged += HandleIntChanged;
			Status.OnValueChanged += HandleStatusChanged;
			HasActed.OnValueChanged += HandleBoolChanged;
			HandRevealed.OnValueChanged += HandleHandRevealedChanged;
			Health.OnValueChanged += HandleHealthChanged;
			LookedAtHoleCards.OnValueChanged += HandleLookedAtChanged;
			HallucinationRate.OnValueChanged += HandleHallucinationChanged;

			HoleCards.OnListChanged += HandleHoleCardsChanged;

			OnHandVisibilityRulesChanged += HandleVisibilityRulesChanged;
		}

		public override void OnNetworkDespawn()
		{
			SeatIndex.OnValueChanged -= HandleIntChanged;
			Status.OnValueChanged -= HandleStatusChanged;
			HasActed.OnValueChanged -= HandleBoolChanged;
			HandRevealed.OnValueChanged -= HandleHandRevealedChanged;
			Health.OnValueChanged -= HandleHealthChanged;
			HallucinationRate.OnValueChanged -= HandleHallucinationChanged;
			LookedAtHoleCards.OnValueChanged -= HandleLookedAtChanged;

			HoleCards.OnListChanged -= HandleHoleCardsChanged;

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

		// The status alone: the cards stay where they are, and IsFolded is what turns them face down and puts
		// them back on the table. The next deal's ServerResetForHand is what takes them away.
		public void ServerFold()
		{
			if (!IsServer || !IsInHand) return;

			Status.Value = PokerPlayerStatus.Folded;
			HasActed.Value = true;
		}

		public void ServerResetForHand()
		{
			if (!IsServer) return;

			HasActed.Value = false;
			HandRevealed.Value = false;
			HoleCards.Clear();

			// A new hand is fresh cards nobody has dared to look at yet.
			LookedAtHoleCards.Value = 0;
		}

		// A new match rather than a new hand. Blood and hallucination are what a match is played *with* —
		// they carry from hand to hand and going under is how a player stops being dealt in — so this is the
		// one place they go back, and it belongs to the table going idle rather than to any hand ending.
		public void ServerResetForMatch()
		{
			if (!IsServer) return;

			ServerResetForHand();

			ServerResetHealthToStart();
			HallucinationRate.Value = 0;
			Status.Value = PokerPlayerStatus.Waiting;
			InMatch.Value = false;
		}

		public void ServerResetHealthToStart()
		{
			if (!IsServer) return;

			Health.Value = StartingHealth;
		}

		public void ServerResetForRound()
		{
			if (!IsServer) return;

			HasActed.Value = false;
		}

		public void ServerSetHoleCards(IReadOnlyList<CardData> cards)
		{
			if (!IsServer) return;

			HoleCards.Clear();
			foreach (var card in cards) HoleCards.Add(card);
		}

		// Turning your own cards over — the whole chosen set in one ask, one bit per slot. Asked of the
		// server rather than decided locally: the limit is a rule, and a client that lied about it would be
		// reading a card the round says it may not.
		//
		// One ask and one write because the player made one decision. Sent a slot at a time, the host
		// heard each pick as its own change in whatever order the picks were made while a client heard
		// the set at once — so anything drawn off the change (which card flies to the hand first) came out
		// differently on the two, and the gesture played once per card. Refused whole, never in part, and
		// it says which gate refused it.
		[Rpc(SendTo.Server)]
		public void LookAtHoleCardsRPC(int slots)
		{
			var refusal = RefuseLook(slots);
			if (refusal != null)
			{
				Debug.LogWarning($"[{nameof(PokerPlayerData)}] Look at hole cards refused for seat {SeatIndex.Value}: {refusal}.", this);
				return;
			}

			LookedAtHoleCards.Value |= slots;

			// Reaching for the cards is something the table watches, so the gesture is played on the server for
			// everyone rather than locally by whoever pressed. A state the art has not landed yet is skipped
			// silently by PlayerActionAnimator, so this can be wired before there is anything to play.
			GetComponent<PokerPlayer>()?.ActionAnimator?.ServerPlay(PlayerActionIds.PickUpCard);
		}

		private string RefuseLook(int slots)
		{
			if (slots == 0) return "no cards named";

			var count = 0;
			for (var slot = 0; slot < 31; slot++)
			{
				if ((slots & (1 << slot)) == 0) continue;
				if (!CanLookAt(slot)) return $"slot {slot} may not be looked at";

				count++;
			}

			return LookedAtCount + count > ViewableHoleCards.Value ? "more cards than the round allows" : null;
		}

		// The whole hand held at once, for a round where every card dealt is the holder's to see and nobody
		// chooses which. Written by the deal before the cards, so they are built in the hand rather than on the
		// table. One write, so every screen holds the same set.
		public void ServerPickUpHoleCards(int count)
		{
			if (!IsServer || count <= 0) return;

			LookedAtHoleCards.Value = (1 << Mathf.Min(count, 31)) - 1;
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

		private void HandleIntChanged(int previous, int current) => OnStateChanged?.Invoke();

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

		private void HandleBoolChanged(bool previous, bool current) => OnStateChanged?.Invoke();
		// Folding is how the cards read as well as a fact about the player, so it raises both.
		private void HandleStatusChanged(PokerPlayerStatus previous, PokerPlayerStatus current)
		{
			OnStateChanged?.Invoke();
			OnHoleCardPresentationChanged?.Invoke();
		}
		private void HandleHoleCardsChanged(NetworkListEvent<CardData> changeEvent) => OnHoleCardsChanged?.Invoke(changeEvent);
	}
}