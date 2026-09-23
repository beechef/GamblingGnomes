using System;
using Game.Runtime.GameMode.Poker.Items;
using Unity.Netcode;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Player
{
	// The item cards one player is holding. Which cards is theirs alone to read; how many is public, so the
	// table can see who is armed without learning with what. Only the server writes it — dealing, using and
	// the end of a match all go through PokerItemModule.
	public class PokerItemInventory : NetworkBehaviour
	{
		public readonly NetworkList<PokerItemUnit> Items = new(null,
			NetworkVariableReadPermission.Owner,
			NetworkVariableWritePermission.Server);

		[HideInInspector] public NetworkVariable<int> Count = new(0,
			readPerm: NetworkVariableReadPermission.Everyone,
			writePerm: NetworkVariableWritePermission.Server);

		// The street serial the player last used an item on, and how many they used there. A serial rather
		// than a flag, so nothing has to go round clearing it when the next street opens.
		[HideInInspector] public NetworkVariable<int> UsedStreetSerial = new(-1,
			readPerm: NetworkVariableReadPermission.Everyone,
			writePerm: NetworkVariableWritePermission.Server);

		[HideInInspector] public NetworkVariable<int> UsesOnStreet = new(0,
			readPerm: NetworkVariableReadPermission.Everyone,
			writePerm: NetworkVariableWritePermission.Server);

		// Raised on the server and on the owner; nobody else can see the list change.
		public event Action OnItemsChanged;
		public event Action OnCountChanged;
		public event Action OnUsesChanged;

		public override void OnNetworkSpawn()
		{
			Items.OnListChanged += HandleItemsChanged;
			Count.OnValueChanged += HandleCountChanged;
			UsedStreetSerial.OnValueChanged += HandleUsesChanged;
			UsesOnStreet.OnValueChanged += HandleUsesChanged;
		}

		public override void OnNetworkDespawn()
		{
			UsesOnStreet.OnValueChanged -= HandleUsesChanged;
			UsedStreetSerial.OnValueChanged -= HandleUsesChanged;
			Count.OnValueChanged -= HandleCountChanged;
			Items.OnListChanged -= HandleItemsChanged;
		}

		private void HandleItemsChanged(NetworkListEvent<PokerItemUnit> change) => OnItemsChanged?.Invoke();
		private void HandleCountChanged(int previous, int current) => OnCountChanged?.Invoke();
		private void HandleUsesChanged(int previous, int current) => OnUsesChanged?.Invoke();

		public bool Holds(PokerItemType type)
		{
			foreach (var unit in Items)
			{
				if (unit.Type == type) return true;
			}

			return false;
		}

		public int UsesOn(int streetSerial) => UsedStreetSerial.Value == streetSerial ? UsesOnStreet.Value : 0;

		public bool ServerGive(PokerItemType type, int capacity)
		{
			if (!IsServer || type == PokerItemType.None || Items.Count >= capacity) return false;

			Items.Add(type);
			Count.Value = Items.Count;
			return true;
		}

		public bool ServerTake(PokerItemType type)
		{
			if (!IsServer) return false;

			for (var i = 0; i < Items.Count; i++)
			{
				if (Items[i].Type != type) continue;

				Items.RemoveAt(i);
				Count.Value = Items.Count;
				return true;
			}

			return false;
		}

		public void ServerRecordUse(int streetSerial)
		{
			if (!IsServer) return;

			UsesOnStreet.Value = UsesOn(streetSerial) + 1;
			UsedStreetSerial.Value = streetSerial;
		}

		public void ServerResetForMatch()
		{
			if (!IsServer) return;

			if (Items.Count > 0) Items.Clear();
			Count.Value = 0;
			UsedStreetSerial.Value = -1;
			UsesOnStreet.Value = 0;
		}
	}
}
