using System;
using Game.Runtime.GameMode.Poker.Items;
using Unity.Netcode;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Player
{
	// What this player has learned through items this hand. Owner-read: finding something out is the whole
	// point of the card, and the rest of the table only hears that it was played. Forgotten when the hand
	// is put away.
	public class PokerItemKnowledge : NetworkBehaviour
	{
		[HideInInspector] public NetworkVariable<PokerSuitCounts> SuitCounts = new(default,
			readPerm: NetworkVariableReadPermission.Owner,
			writePerm: NetworkVariableWritePermission.Server);

		// Raised on the server and on the owner.
		public event Action OnKnowledgeChanged;

		public override void OnNetworkSpawn()
		{
			SuitCounts.OnValueChanged += HandleSuitCountsChanged;
		}

		public override void OnNetworkDespawn()
		{
			SuitCounts.OnValueChanged -= HandleSuitCountsChanged;
		}

		private void HandleSuitCountsChanged(PokerSuitCounts previous, PokerSuitCounts current) => OnKnowledgeChanged?.Invoke();

		public void ServerSetSuitCounts(PokerSuitCounts counts)
		{
			if (!IsServer) return;

			counts.IsKnown = true;
			SuitCounts.Value = counts;
		}

		public void ServerResetForHand()
		{
			if (!IsServer) return;

			SuitCounts.Value = default;
		}
	}
}
