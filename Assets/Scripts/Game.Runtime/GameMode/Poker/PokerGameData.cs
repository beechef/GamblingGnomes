using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker
{
	// Table state only. Anything belonging to a single player lives on that player's PokerPlayerData,
	// which is also what keeps hole cards out of everyone else's copy of the game.
	public class PokerGameData : NetworkBehaviour
	{
		public const ulong NoTurn = ulong.MaxValue;

		[HideInInspector] public NetworkVariable<PokerPhase> Phase = new(PokerPhase.Waiting,
			readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);

		[HideInInspector] public NetworkVariable<FixedString32Bytes> StageId = new(default,
			readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);

		// Empty while nothing is overlaid. Overlays pause the stage underneath rather than replacing
		// it, so the UI needs both ids to know what to draw on top of what.
		[HideInInspector] public NetworkVariable<FixedString32Bytes> OverlayStageId = new(default,
			readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);

		[HideInInspector] public NetworkVariable<int> Pot = new(0,
			readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);

		[HideInInspector] public NetworkVariable<int> CurrentBet = new(0,
			readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);

		[HideInInspector] public NetworkVariable<int> LastRaise = new(0,
			readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);

		[HideInInspector] public NetworkVariable<int> DealerSeatIndex = new(-1,
			readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);

		[HideInInspector] public NetworkVariable<ulong> CurrentTurnClientId = new(NoTurn,
			readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);

		[HideInInspector] public NetworkVariable<double> TurnEndTime = new(0d,
			readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);

		[HideInInspector] public NetworkVariable<float> TurnDuration = new(0f,
			readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);

		[HideInInspector] public NetworkVariable<ulong> LastWinnerClientId = new(NoTurn,
			readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);

		// Change-only on purpose, like the gesture events: a late joiner receives the last announcement as
		// spawned state, and an action from half a hand ago is not worth flashing at them.
		[HideInInspector] public NetworkVariable<PokerActionNotice> ActionNotice = new(default,
			readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);

		// One clock any stage can run, separate from the turn clock: a deal that plays out, a showdown
		// that lingers, a street everybody bets on at once — none of them belong to a single seat.
		[HideInInspector] public NetworkVariable<double> StageEndTime = new(0d,
			readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);

		[HideInInspector] public NetworkVariable<float> StageDuration = new(0f,
			readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);

		// The whole board, real cards, on the table from the deal — the same trade the hole cards make.
		// Which of them may be looked at is a display rule, so an ability that reads the river ahead of
		// the table is a change of rule and not a change of plumbing. The cost is that a modified client
		// can read the list, so the rule is the only thing keeping the board face down.
		public readonly NetworkList<CardData> CommunityCards = new(null,
			NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

		// How much of the board the whole table has been shown. Cards past this are lying face down: still
		// on the table, still dealt, and nobody's business but whatever earns a look at them.
		[HideInInspector] public NetworkVariable<int> RevealedCommunityCards = new(0,
			readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);

		// Installed by whatever grants a look at a card the street has not turned yet — a cheat ability, a
		// spectator mode, a debug view. A list rather than a single slot so two of them can coexist; any
		// one saying yes is enough.
		private static readonly List<Func<int, bool>> CommunityVisibilityProviders = new();

		public static void AddCommunityVisibilityProvider(Func<int, bool> provider)
		{
			if (provider != null && !CommunityVisibilityProviders.Contains(provider)) CommunityVisibilityProviders.Add(provider);
		}

		public static void RemoveCommunityVisibilityProvider(Func<int, bool> provider)
		{
			CommunityVisibilityProviders.Remove(provider);
		}

		// Raised by a provider whose answer has just changed. Nothing about the board itself moved, only
		// who is allowed to look at it, so the view has no other way of noticing.
		public static event Action OnCommunityVisibilityRulesChanged;

		public static void NotifyCommunityVisibilityRulesChanged() => OnCommunityVisibilityRulesChanged?.Invoke();

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void ResetStatics()
		{
			CommunityVisibilityProviders.Clear();
			OnCommunityVisibilityRulesChanged = null;
		}

		public bool IsCommunityCardVisible(int index)
		{
			if (index < 0) return false;
			if (index < RevealedCommunityCards.Value) return true;

			foreach (var provider in CommunityVisibilityProviders)
			{
				if (provider != null && provider.Invoke(index)) return true;
			}

			return false;
		}

		// The showdown board, in finishing order. Filled when the hand is settled and cleared when the
		// next one is dealt, so a panel can simply mirror it rather than recompute the ranking.
		public readonly NetworkList<PokerShowdownEntry> Showdown = new(null,
			NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

		public event Action OnShowdownChanged;

		// The change itself travels with the event so a view can add or remove the one card that moved
		// instead of rebuilding a board whose other cards are mid animation.
		public event Action<NetworkListEvent<CardData>> OnCommunityCardsChanged;

		// The board did not change, but what may be seen of it did — a street opening, or a rule granting
		// somebody a look. Carries nothing: a view has to re-ask about every card either way.
		public event Action OnCommunityVisibilityChanged;

		public bool HasTurn => CurrentTurnClientId.Value != NoTurn;

		public float TurnRemaining
		{
			get
			{
				if (!HasTurn || TurnDuration.Value <= 0f || !NetworkManager.Singleton) return 0f;

				var remaining = TurnEndTime.Value - NetworkManager.Singleton.ServerTime.Time;
				return Mathf.Clamp((float)remaining, 0f, TurnDuration.Value);
			}
		}

		public float TurnNormalized => TurnDuration.Value <= 0f ? 0f : TurnRemaining / TurnDuration.Value;

		public bool HasStageTimer => StageDuration.Value > 0f;

		public float StageTimeRemaining
		{
			get
			{
				if (!HasStageTimer || !NetworkManager.Singleton) return 0f;

				var remaining = StageEndTime.Value - NetworkManager.Singleton.ServerTime.Time;
				return Mathf.Clamp((float)remaining, 0f, StageDuration.Value);
			}
		}

		public float StageTimeNormalized => StageDuration.Value <= 0f ? 0f : StageTimeRemaining / StageDuration.Value;

		public override void OnNetworkSpawn()
		{
			CommunityCards.OnListChanged += HandleCommunityCardsChanged;
			Showdown.OnListChanged += HandleShowdownChanged;
			RevealedCommunityCards.OnValueChanged += HandleRevealedCountChanged;

			OnCommunityVisibilityRulesChanged += HandleVisibilityRulesChanged;
		}

		public override void OnNetworkDespawn()
		{
			OnCommunityVisibilityRulesChanged -= HandleVisibilityRulesChanged;

			RevealedCommunityCards.OnValueChanged -= HandleRevealedCountChanged;
			CommunityCards.OnListChanged -= HandleCommunityCardsChanged;
			Showdown.OnListChanged -= HandleShowdownChanged;
		}

		private void HandleCommunityCardsChanged(NetworkListEvent<CardData> changeEvent) => OnCommunityCardsChanged?.Invoke(changeEvent);
		private void HandleShowdownChanged(NetworkListEvent<PokerShowdownEntry> changeEvent) => OnShowdownChanged?.Invoke();
		private void HandleRevealedCountChanged(int previous, int current) => OnCommunityVisibilityChanged?.Invoke();
		private void HandleVisibilityRulesChanged() => OnCommunityVisibilityChanged?.Invoke();
	}
}
