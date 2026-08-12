using System;
using System.Collections.Generic;
using Game.Runtime.Player;
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

		[Tooltip("The wallet the bets come out of. Lives beside this on the player, not on the table.")]
		[SerializeField] private PlayerData _wallet;

		private static readonly List<PokerPlayer> Registry = new();

		public static IReadOnlyList<PokerPlayer> All => Registry;
		public static event Action OnRegistryChanged;

		// The player this client owns. UI hangs its whole lifecycle off this rather than polling for a
		// local player to appear.
		public static PokerPlayer Local { get; private set; }
		public static event Action<PokerPlayer> OnLocalPlayerChanged;

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void ResetStatics()
		{
			Registry.Clear();
			Local = null;
			OnRegistryChanged = null;
			OnLocalPlayerChanged = null;
		}

		public PokerPlayerData Data => _data;
		public PlayerData Wallet => _wallet;
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
			if (!_wallet) _wallet = GetComponent<PlayerData>();

			if (!Registry.Contains(this))
			{
				Registry.Add(this);
				OnRegistryChanged?.Invoke();
			}

			// Seating happens on the server and arrives here as a replicated seat index. Without this
			// the seated list only ever rebuilt on the host, so a client never saw itself at the table
			// and never got an action bar on its turn.
			if (_data) _data.SeatIndex.OnValueChanged += HandleSeatIndexChanged;

			if (!IsOwner) return;

			Local = this;
			OnLocalPlayerChanged?.Invoke(this);
		}

		public override void OnNetworkDespawn()
		{
			if (_data) _data.SeatIndex.OnValueChanged -= HandleSeatIndexChanged;

			if (Registry.Remove(this)) OnRegistryChanged?.Invoke();

			if (Local != this) return;

			Local = null;
			OnLocalPlayerChanged?.Invoke(null);
		}

		private void HandleSeatIndexChanged(int previous, int current) => OnRegistryChanged?.Invoke();
	}
}
