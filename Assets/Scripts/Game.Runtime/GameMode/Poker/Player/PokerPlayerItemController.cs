using System;
using Game.Runtime.GameMode.Poker.Items;
using Sirenix.OdinInspector;
using Unity.Netcode;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Player
{
	// What this player has been served and what they have already swallowed. Lifted off PokerPlayerData,
	// which had grown into every verb the round has: a data class owns state, and "eat this and charge
	// the right price for it" is a rule with a decision in it.
	//
	// The plate and the record live together on purpose. Which kinds somebody has met is what sets the
	// price of the next one, so a record kept anywhere else is a second value to keep in step with the
	// eating that writes it.
	public class PokerPlayerItemController : NetworkBehaviour
	{
		// What this player still has to swallow, in the order it goes down. Everyone-read, because the
		// whole point of the eating is that the table watches it happen — and a plate somebody is still
		// working through is the clearest read of how badly the hand went for them.
		public readonly NetworkList<byte> Pending = new(null,
			NetworkVariableReadPermission.Everyone,
			NetworkVariableWritePermission.Server);

		// One bit per kind they have already met, lasting the whole match. Public for the same reason the
		// rate is: what a player can still be hurt by is part of reading them.
		[HideInInspector] public NetworkVariable<int> ConsumedTypes = new(0,
			readPerm: NetworkVariableReadPermission.Everyone,
			writePerm: NetworkVariableWritePermission.Server);

		[Header("References")]
		[Required]
		[Tooltip("Where the price of an item lands. The rate is the player's, not this controller's.")]
		[SerializeField] private PokerPlayerData _data;

		public event Action OnChanged;

		public int PendingCount => Pending.Count;

		public override void OnNetworkSpawn()
		{
			if (!_data) _data = GetComponentInParent<PokerPlayerData>();

			Pending.OnListChanged += HandlePendingChanged;
			ConsumedTypes.OnValueChanged += HandleConsumedChanged;
		}

		public override void OnNetworkDespawn()
		{
			ConsumedTypes.OnValueChanged -= HandleConsumedChanged;
			Pending.OnListChanged -= HandlePendingChanged;
		}

		// Put on the plate. The kind rather than the effect, the same trade every stake makes: an item
		// replicates as its index and the database turns it back into what eating it does.
		public void ServerServe(byte itemType)
		{
			if (!IsServer) return;

			Pending.Add(itemType);
		}

		// The next bite, taken off the plate as it is swallowed rather than after — the list is what the
		// visual draws, so an item still on it whose effect has already landed is one the table can see
		// that nobody is going to eat.
		public bool ServerTakeNext(out byte itemType)
		{
			itemType = PokerItemDatabase.PlainChip;
			if (!IsServer || Pending.Count == 0) return false;

			itemType = Pending[0];
			Pending.RemoveAt(0);
			return true;
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

		// A plate belongs to the match it was served in, and so does the record of what has been met.
		// Not swept per hand: the eating happens between one hand and the next, so a hand reset would
		// clear the very items that beat is about to serve.
		public void ServerResetForMatch()
		{
			if (!IsServer) return;

			Pending.Clear();
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

		private void HandlePendingChanged(NetworkListEvent<byte> change) => OnChanged?.Invoke();
		private void HandleConsumedChanged(int previous, int current) => OnChanged?.Invoke();
	}
}
