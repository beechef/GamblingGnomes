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

		// The pot: one entry per cap on the table, stamped with who it stands in front of, on which street it
		// went up and what kind it is. Only PokerTableUtility writes it.
		public readonly NetworkList<PokerPotEntry> PotEntries = new(null,
			NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

		// How many chairs this table is laid with. Replicated rather than each client reading the lobby
		// for itself: where the chairs stand has to be the same picture on every screen, and a client
		// whose copy of the lobby says something else would draw the table wrong with nothing to warn it.
		[HideInInspector] public NetworkVariable<int> ActiveSeatCount = new(0,
			readPerm: NetworkVariableReadPermission.Everyone,
			writePerm: NetworkVariableWritePermission.Server);

		[HideInInspector] public NetworkVariable<ulong> CurrentTurnClientId = new(NoTurn,
			readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);

		// Who the table is watching, which is a different question from who is being asked to move. A beat
		// where everybody swallows what the hand cost them gives nobody a turn — nothing is being asked —
		// and is still the moment every head in the room should be pointed at one player. Reading the focus
		// off the turn works right up until such a beat exists, and then a bar somewhere announces "X'S
		// TURN" over a player who has not been offered anything.
		[HideInInspector] public NetworkVariable<ulong> FocusClientId = new(NoTurn,
			readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);

		[HideInInspector] public NetworkVariable<double> TurnEndTime = new(0d,
			readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);

		[HideInInspector] public NetworkVariable<float> TurnDuration = new(0f,
			readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);

		[HideInInspector] public NetworkVariable<ulong> LastWinnerClientId = new(NoTurn,
			readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);

		// Who outlasted the match, announced while the phase is MatchOver. NoTurn when nobody did.
		[HideInInspector] public NetworkVariable<ulong> SurvivorClientId = new(NoTurn,
			readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);

		// The table-wide blink that hides the match being put back. On while every screen should be shut;
		// the server resets the table only after raising it, so the reset lands behind a closed eye.
		[HideInInspector] public NetworkVariable<bool> MatchResetting = new(false,
			readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);

		// One clock any stage can run, separate from the turn clock: a deal that plays out, a showdown
		// that lingers, a beat the whole table answers at once — none of them belong to a single seat.
		[HideInInspector] public NetworkVariable<double> StageEndTime = new(0d,
			readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);

		[HideInInspector] public NetworkVariable<float> StageDuration = new(0f,
			readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);

		// The showdown board, in finishing order. Filled when the hand is settled and cleared when the
		// showdown hands over, so a panel can simply mirror it rather than recompute the ranking.
		public readonly NetworkList<PokerShowdownEntry> Showdown = new(null,
			NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

		// The board, dealt face down with the hand and turned over street by street. Everyone sees the same
		// cards, so it is one list on the table; which of them are face up is the only other fact.
		public readonly NetworkList<CardData> CommunityCards = new(null,
			NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

		// One bit per board slot turned face up for the whole table. A mask rather than a count, because an
		// item can turn a card out of street order.
		[HideInInspector] public NetworkVariable<int> RevealedCommunityMask = new(0,
			readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);

		// How many cards are still in the undealt deck. The deck itself lives on the server only; the count is
		// public, and anything a client offers on it (Extra Draw) reads it here.
		[HideInInspector] public NetworkVariable<int> DeckRemaining = new(0,
			readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);

		public event Action<NetworkListEvent<CardData>> OnCommunityCardsChanged;
		public event Action OnCommunityRevealChanged;

		// Face up for the whole table. What a hand is scored with, whatever any one screen was shown besides.
		public bool IsCommunityCardRevealed(int index) =>
			index >= 0 && index < CommunityCards.Count && index < 31 && (RevealedCommunityMask.Value & (1 << index)) != 0;

		public bool IsCommunityCardVisible(int index)
		{
			if (index < 0 || index >= CommunityCards.Count) return false;
			if (IsCommunityCardRevealed(index)) return true;

			foreach (var provider in CommunityVisibilityProviders)
			{
				if (provider != null && provider.Invoke(index)) return true;
			}

			return false;
		}

		// Installed by whatever shows this client a board card before the street turns it — an item that
		// peeked at one. Local sight only; the table's own reveal is RevealedCommunityMask.
		private static readonly List<Func<int, bool>> CommunityVisibilityProviders = new();

		public static void AddCommunityVisibilityProvider(Func<int, bool> provider)
		{
			if (provider != null && !CommunityVisibilityProviders.Contains(provider)) CommunityVisibilityProviders.Add(provider);
		}

		public static void RemoveCommunityVisibilityProvider(Func<int, bool> provider) => CommunityVisibilityProviders.Remove(provider);

		// Raised by a provider whose answer has just changed; nothing about the board did.
		public static event Action OnCommunityVisibilityRulesChanged;

		public static void NotifyCommunityVisibilityRulesChanged() => OnCommunityVisibilityRulesChanged?.Invoke();

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void ResetStatics()
		{
			CommunityVisibilityProviders.Clear();
			OnCommunityVisibilityRulesChanged = null;
		}

		public event Action OnShowdownChanged;

		// The change travels with the event, so a view can animate the one cap that arrived rather than
		// rebuilding a pile that is mid flight.
		public event Action<NetworkListEvent<PokerPotEntry>> OnPotEntriesChanged;

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

		// Whether this turn is on a clock at all. A stage with no duration is one the table waits on, so
		// the bar is hidden rather than drawn sitting at zero — which reads as a hung timer.
		public bool HasTurnClock => HasTurn && TurnDuration.Value > 0f;

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
			Showdown.OnListChanged += HandleShowdownChanged;
			PotEntries.OnListChanged += HandlePotEntriesChanged;
			CommunityCards.OnListChanged += HandleCommunityCardsChanged;
			RevealedCommunityMask.OnValueChanged += HandleCommunityRevealChanged;
		}

		public override void OnNetworkDespawn()
		{
			RevealedCommunityMask.OnValueChanged -= HandleCommunityRevealChanged;
			CommunityCards.OnListChanged -= HandleCommunityCardsChanged;
			PotEntries.OnListChanged -= HandlePotEntriesChanged;
			Showdown.OnListChanged -= HandleShowdownChanged;
		}

		private void HandleCommunityCardsChanged(NetworkListEvent<CardData> changeEvent) => OnCommunityCardsChanged?.Invoke(changeEvent);
		private void HandleCommunityRevealChanged(int previous, int current) => OnCommunityRevealChanged?.Invoke();
		private void HandlePotEntriesChanged(NetworkListEvent<PokerPotEntry> changeEvent) => OnPotEntriesChanged?.Invoke(changeEvent);
		private void HandleShowdownChanged(NetworkListEvent<PokerShowdownEntry> changeEvent) => OnShowdownChanged?.Invoke();
	}
}
