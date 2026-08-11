using System;
using System.Collections.Generic;
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

		[HideInInspector] public NetworkVariable<int> SeatIndex = new(NoSeat,
			readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);

		[HideInInspector] public NetworkVariable<int> Chips = new(0,
			readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);

		[HideInInspector] public NetworkVariable<int> Bet = new(0,
			readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);

		[HideInInspector] public NetworkVariable<int> TotalBet = new(0,
			readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);

		[HideInInspector] public NetworkVariable<PokerPlayerStatus> Status = new(PokerPlayerStatus.Waiting,
			readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);

		[HideInInspector] public NetworkVariable<bool> HasActed = new(false,
			readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);

		// Showdown, or anything else that decides this hand is public.
		[HideInInspector] public NetworkVariable<bool> HandRevealed = new(false,
			readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);

		// Replicated to everyone rather than owner-only: who may look is a display rule, so an ability
		// that shows someone else's hand is a change of rule and not a change of plumbing. The trade is
		// that a modified client can read the list, so the rule is the only thing hiding it.
		public readonly NetworkList<CardData> HoleCards = new(null,
			NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

		// Installed by whatever grants extra sight — a cheat ability, a spectator mode, a debug view.
		public static Func<PokerPlayerData, bool> HandVisibilityOverride;

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void ResetStatics() => HandVisibilityOverride = null;

		public event Action OnStateChanged;
		public event Action OnHoleCardsChanged;

		public bool IsSeated => SeatIndex.Value != NoSeat;
		public bool IsInHand => Status.Value is PokerPlayerStatus.Active or PokerPlayerStatus.AllIn;
		public bool CanAct => Status.Value == PokerPlayerStatus.Active;
		public int CardCount => HoleCards.Count;

		public bool IsHandVisible => IsOwner
		                             || HandRevealed.Value
		                             || (HandVisibilityOverride != null && HandVisibilityOverride.Invoke(this));

		public override void OnNetworkSpawn()
		{
			SeatIndex.OnValueChanged += HandleIntChanged;
			Chips.OnValueChanged += HandleIntChanged;
			Bet.OnValueChanged += HandleIntChanged;
			TotalBet.OnValueChanged += HandleIntChanged;
			Status.OnValueChanged += HandleStatusChanged;
			HasActed.OnValueChanged += HandleBoolChanged;
			HandRevealed.OnValueChanged += HandleBoolChanged;

			HoleCards.OnListChanged += HandleHoleCardsChanged;
		}

		public override void OnNetworkDespawn()
		{
			SeatIndex.OnValueChanged -= HandleIntChanged;
			Chips.OnValueChanged -= HandleIntChanged;
			Bet.OnValueChanged -= HandleIntChanged;
			TotalBet.OnValueChanged -= HandleIntChanged;
			Status.OnValueChanged -= HandleStatusChanged;
			HasActed.OnValueChanged -= HandleBoolChanged;
			HandRevealed.OnValueChanged -= HandleBoolChanged;

			HoleCards.OnListChanged -= HandleHoleCardsChanged;
		}

		public void ServerTakeSeat(int seatIndex, int startingChips)
		{
			if (!IsServer) return;

			SeatIndex.Value = seatIndex;
			Chips.Value = startingChips;
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
			HoleCards.Clear();
		}

		public void ServerResetForRound()
		{
			if (!IsServer) return;

			Bet.Value = 0;
			HasActed.Value = false;
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

			var paid = Mathf.Clamp(amount, 0, Chips.Value);

			Chips.Value -= paid;
			Bet.Value += paid;
			TotalBet.Value += paid;

			if (Chips.Value <= 0) Status.Value = PokerPlayerStatus.AllIn;

			return paid;
		}

		public void ServerCollectBet()
		{
			if (!IsServer) return;

			Bet.Value = 0;
		}

		private void HandleIntChanged(int previous, int current) => OnStateChanged?.Invoke();
		private void HandleBoolChanged(bool previous, bool current) => OnStateChanged?.Invoke();
		private void HandleStatusChanged(PokerPlayerStatus previous, PokerPlayerStatus current) => OnStateChanged?.Invoke();
		private void HandleHoleCardsChanged(NetworkListEvent<CardData> changeEvent) => OnHoleCardsChanged?.Invoke();
	}
}
