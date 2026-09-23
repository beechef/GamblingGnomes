using System;
using Game.Runtime.GameMode.Poker.Items;
using Unity.Netcode;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Player
{
	// What this player has learned through items this hand, and what others learned of theirs. Owner-read:
	// finding something out is the whole point of the card, and the rest of the table only hears that it
	// was played. Forgotten when the hand is put away.
	//
	// On the owner's machine it also turns what it knows face up, through the per-card visibility providers,
	// so a peeked card lying on the table reads like any card the owner may see.
	public class PokerItemKnowledge : NetworkBehaviour
	{
		[HideInInspector] public NetworkVariable<PokerSuitCounts> SuitCounts = new(default,
			readPerm: NetworkVariableReadPermission.Owner,
			writePerm: NetworkVariableWritePermission.Server);

		// Cards of other players, or of the board, this player has been shown privately.
		public readonly NetworkList<PokerKnownCard> KnownCards = new(null,
			NetworkVariableReadPermission.Owner,
			NetworkVariableWritePermission.Server);

		// This player's own cards somebody else was shown privately, and who.
		public readonly NetworkList<PokerKnownCard> ExposedCards = new(null,
			NetworkVariableReadPermission.Owner,
			NetworkVariableWritePermission.Server);

		// Raised on the server and on the owner.
		public event Action OnKnowledgeChanged;

		private bool _providing;

		public override void OnNetworkSpawn()
		{
			SuitCounts.OnValueChanged += HandleSuitCountsChanged;
			KnownCards.OnListChanged += HandleKnownCardsChanged;
			ExposedCards.OnListChanged += HandleExposedCardsChanged;

			if (!IsOwner) return;

			_providing = true;
			PokerPlayerData.AddHandVisibilityProvider(IsHoleCardKnown);
			PokerGameData.AddCommunityVisibilityProvider(IsBoardCardKnown);
		}

		public override void OnNetworkDespawn()
		{
			if (_providing)
			{
				_providing = false;
				PokerGameData.RemoveCommunityVisibilityProvider(IsBoardCardKnown);
				PokerPlayerData.RemoveHandVisibilityProvider(IsHoleCardKnown);
				NotifyVisibilityChanged();
			}

			ExposedCards.OnListChanged -= HandleExposedCardsChanged;
			KnownCards.OnListChanged -= HandleKnownCardsChanged;
			SuitCounts.OnValueChanged -= HandleSuitCountsChanged;
		}

		private void HandleSuitCountsChanged(PokerSuitCounts previous, PokerSuitCounts current) => OnKnowledgeChanged?.Invoke();

		private void HandleKnownCardsChanged(NetworkListEvent<PokerKnownCard> change)
		{
			if (_providing) NotifyVisibilityChanged();
			OnKnowledgeChanged?.Invoke();
		}

		private void HandleExposedCardsChanged(NetworkListEvent<PokerKnownCard> change) => OnKnowledgeChanged?.Invoke();

		private static void NotifyVisibilityChanged()
		{
			PokerPlayerData.NotifyHandVisibilityRulesChanged();
			PokerGameData.NotifyCommunityVisibilityRulesChanged();
		}

		// Only while the card is still the one that was seen: a card swapped in since is a card nobody showed them.
		private bool IsHoleCardKnown(PokerPlayerData holder, int slot)
		{
			if (!holder || slot < 0 || slot >= holder.HoleCards.Count) return false;

			foreach (var known in KnownCards)
			{
				if (known.OtherClientId == holder.OwnerClientId && known.Slot == slot && known.Card.Equals(holder.HoleCards[slot])) return true;
			}

			return false;
		}

		private bool IsBoardCardKnown(int slot)
		{
			foreach (var known in KnownCards)
			{
				if (known.IsBoard && known.Slot == slot) return true;
			}

			return false;
		}

		public bool Knows(ulong otherClientId, int slot)
		{
			foreach (var known in KnownCards)
			{
				if (known.OtherClientId == otherClientId && known.Slot == slot) return true;
			}

			return false;
		}

		public void ServerSetSuitCounts(PokerSuitCounts counts)
		{
			if (!IsServer) return;

			counts.IsKnown = true;
			SuitCounts.Value = counts;
		}

		public void ServerLearn(ulong otherClientId, int slot, CardData card)
		{
			if (!IsServer) return;

			KnownCards.Add(new PokerKnownCard { OtherClientId = otherClientId, Slot = slot, Card = card });
		}

		public void ServerRecordExposure(ulong viewerClientId, int slot, CardData card)
		{
			if (!IsServer) return;

			ExposedCards.Add(new PokerKnownCard { OtherClientId = viewerClientId, Slot = slot, Card = card });
		}

		// The slot holds another card now, so what was seen there is no longer true.
		public void ServerForgetCard(ulong otherClientId, int slot)
		{
			if (!IsServer) return;

			for (var i = KnownCards.Count - 1; i >= 0; i--)
			{
				if (KnownCards[i].OtherClientId == otherClientId && KnownCards[i].Slot == slot) KnownCards.RemoveAt(i);
			}
		}

		public void ServerForgetExposure(int slot)
		{
			if (!IsServer) return;

			for (var i = ExposedCards.Count - 1; i >= 0; i--)
			{
				if (ExposedCards[i].Slot == slot) ExposedCards.RemoveAt(i);
			}
		}

		public void ServerResetForHand()
		{
			if (!IsServer) return;

			SuitCounts.Value = default;
			if (KnownCards.Count > 0) KnownCards.Clear();
			if (ExposedCards.Count > 0) ExposedCards.Clear();
		}
	}
}
