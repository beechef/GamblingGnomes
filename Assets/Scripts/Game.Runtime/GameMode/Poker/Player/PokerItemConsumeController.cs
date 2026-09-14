using Game.Runtime.GameMode.Poker.Items;
using Unity.Netcode;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Player
{
	// What this player has swallowed, and nothing else. It keeps the record and answers questions about
	// it; what a mouthful *costs* belongs to the effect that was eaten, because two kinds can charge
	// differently for the same record and only the effect knows its own prices.
	//
	// Named for eating rather than for items in general: a stockpile of things a player can choose to use
	// is a different question with a different lifetime, and it will want its own component rather than
	// more verbs on this one.
	public class PokerItemConsumeController : NetworkBehaviour
	{
		// Every kind this player has met, in the order they met them, lasting the whole match. A list of
		// the kinds themselves rather than a bitmask over them: the mask could only say yes or no about
		// the first thirty-one values, needed a static TypeBit helper nobody could read at the call site,
		// and quietly answered "never eaten" for anything past its width. Public for the same reason the
		// rate is — what a player can still be hurt by is part of reading them.
		public readonly NetworkList<PokerItemUnit> Consumed = new(null,
			NetworkVariableReadPermission.Everyone,
			NetworkVariableWritePermission.Server);

		public bool HasConsumed(PokerItemType itemType)
		{
			// A unit with no identity is not a kind anybody can have met before, so it never counts as one.
			if (itemType == PokerItemType.PlainChip) return false;

			foreach (var consumed in Consumed)
			{
				if (consumed == itemType) return true;
			}

			return false;
		}

		// Written after whatever read it, so an effect asking whether this kind is new gets the answer for
		// the mouthful being taken rather than for the one after it. Recorded once: the list says which
		// kinds have been met, not how many times.
		public void ServerRecordConsumed(PokerItemType itemType)
		{
			if (!IsServer || itemType == PokerItemType.PlainChip) return;
			if (HasConsumed(itemType)) return;

			Consumed.Add(itemType);
		}

		// The record belongs to the match it was built up in. Not swept per hand: what a player has met
		// before is what sets the price of the next cap, all match long.
		public void ServerResetForMatch()
		{
			if (!IsServer) return;

			Consumed.Clear();
		}
	}
}
