using Game.Runtime.GameMode.Poker.Items;
using Sirenix.OdinInspector;
using Unity.Netcode;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Player
{
	// What this player has already swallowed. Lifted off PokerPlayerData, which had grown into every verb
	// the round has: a data class owns state, and "eat this and charge the right price for it" is a rule
	// with a decision in it.
	//
	// What is still to be swallowed is not here. It is the pot ledger, standing on the table in front of
	// whoever owns it — the settlement hands the caps over by rewriting their owner, so there is one list
	// of caps in the game and no plate of its own to keep in step with it.
	public class PokerPlayerItemController : NetworkBehaviour
	{
		// One bit per kind they have already met, lasting the whole match. Public for the same reason the
		// rate is: what a player can still be hurt by is part of reading them.
		[HideInInspector] public NetworkVariable<int> ConsumedTypes = new(0,
			readPerm: NetworkVariableReadPermission.Everyone,
			writePerm: NetworkVariableWritePermission.Server);

		[Header("References")]
		[Required]
		[Tooltip("Where the price of an item lands. The rate is the player's, not this controller's.")]
		[SerializeField] private PokerPlayerData _data;

		public override void OnNetworkSpawn()
		{
			if (!_data) _data = GetComponentInParent<PokerPlayerData>();
		}

		// Which kind decides the price: one this player has met before costs the smaller gain, and a kind
		// they have never swallowed costs the larger — which is what makes the winner's choice of what to
		// wager an attack rather than an amount. The record is written here too, so it and the price it
		// sets can never come apart.
		public void ServerConsume(byte itemType, int newTypeGain, int repeatGain)
		{
			if (!IsServer || !_data) return;

			var bit = TypeBit(itemType);
			var metBefore = (ConsumedTypes.Value & bit) != 0;

			ConsumedTypes.Value |= bit;
			_data.ServerChangeHallucination(metBefore ? repeatGain : newTypeGain);
		}

		// The record belongs to the match it was built up in. Not swept per hand: what a player has met
		// before is what sets the price of the next cap, all match long.
		public void ServerResetForMatch()
		{
			if (!IsServer) return;

			ConsumedTypes.Value = 0;
		}

		public bool HasConsumed(byte itemType) => (ConsumedTypes.Value & TypeBit(itemType)) != 0;

		// A kind with no bit of its own — anything past the mask's width, or the plain chip that stands
		// for a unit with no identity — reads as one this player has never met, so an unconfigured table
		// charges the full gain rather than silently charging the smaller one for everything.
		public static int TypeBit(byte itemType)
		{
			if (itemType == PokerItemDatabase.PlainChip || itemType > 31) return 0;

			return 1 << (itemType - 1);
		}
	}
}
