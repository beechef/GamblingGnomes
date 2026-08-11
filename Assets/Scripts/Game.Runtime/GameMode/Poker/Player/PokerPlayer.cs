using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Player
{
	// The poker half of a player, sitting on the player prefab beside the movement and seat pieces.
	// Registering itself here rather than being collected by the mode means the table works no matter
	// which spawns first — a late joiner mid-hand and a mode that spawns after its players both land
	// in the same list.
	public class PokerPlayer : NetworkBehaviour
	{
		[Header("References")]
		[SerializeField] private PokerPlayerData _data;

		private static readonly List<PokerPlayer> Registry = new();

		public static IReadOnlyList<PokerPlayer> All => Registry;
		public static event Action OnRegistryChanged;

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void ResetStatics()
		{
			Registry.Clear();
			OnRegistryChanged = null;
		}

		public PokerPlayerData Data => _data;
		public ulong ClientId => OwnerClientId;

		public static PokerPlayer Find(ulong clientId)
		{
			foreach (var player in Registry)
			{
				if (player && player.ClientId == clientId) return player;
			}

			return null;
		}

		public override void OnNetworkSpawn()
		{
			if (!_data) _data = GetComponent<PokerPlayerData>();

			if (!Registry.Contains(this))
			{
				Registry.Add(this);
				OnRegistryChanged?.Invoke();
			}
		}

		public override void OnNetworkDespawn()
		{
			if (Registry.Remove(this)) OnRegistryChanged?.Invoke();
		}
	}
}
