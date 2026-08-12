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

		public readonly NetworkList<CardData> CommunityCards = new(null,
			NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

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

		public override void OnNetworkSpawn()
		{
			CommunityCards.OnListChanged += HandleCommunityCardsChanged;
		}

		public override void OnNetworkDespawn()
		{
			CommunityCards.OnListChanged -= HandleCommunityCardsChanged;
		}

		private void HandleCommunityCardsChanged(NetworkListEvent<CardData> changeEvent) => OnCommunityCardsChanged?.Invoke(changeEvent);
	}
}
