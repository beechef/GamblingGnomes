using System;
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

		// One clock any stage can run, separate from the turn clock: a deal that plays out, a showdown
		// that lingers, a street everybody bets on at once — none of them belong to a single seat.
		[HideInInspector] public NetworkVariable<double> StageEndTime = new(0d,
			readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);

		[HideInInspector] public NetworkVariable<float> StageDuration = new(0f,
			readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);

		public readonly NetworkList<CardData> CommunityCards = new(null,
			NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

		// The showdown board, in finishing order. Filled when the hand is settled and cleared when the
		// next one is dealt, so a panel can simply mirror it rather than recompute the ranking.
		public readonly NetworkList<PokerShowdownEntry> Showdown = new(null,
			NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

		public event Action OnShowdownChanged;

		// The change itself travels with the event so a view can add or remove the one card that moved
		// instead of rebuilding a board whose other cards are mid animation.
		public event Action<NetworkListEvent<CardData>> OnCommunityCardsChanged;

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
		}

		public override void OnNetworkDespawn()
		{
			CommunityCards.OnListChanged -= HandleCommunityCardsChanged;
			Showdown.OnListChanged -= HandleShowdownChanged;
		}

		private void HandleCommunityCardsChanged(NetworkListEvent<CardData> changeEvent) => OnCommunityCardsChanged?.Invoke(changeEvent);
		private void HandleShowdownChanged(NetworkListEvent<PokerShowdownEntry> changeEvent) => OnShowdownChanged?.Invoke();
	}
}
