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

		[Tooltip("The rig this client renders for this player — where an ability aiming at them finds a hand or a head.")]
		[SerializeField] private PlayerRigController _rig;

		[SerializeField] private PlayerHeadStretchController _headStretch;

		[SerializeField] private PlayerActionAnimator _actionAnimator;

		[SerializeField] private PlayerHandPeekController _handPeek;

		[SerializeField] private PlayerPointController _point;

		[Tooltip("Who draws this player. Marking somebody out for the whole table is a change of how they are drawn, so it is asked of the thing already holding every renderer.")]
		[SerializeField] private PlayerVisual _visual;

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
		public PlayerRigController Rig => _rig;
		public PlayerHeadStretchController HeadStretch => _headStretch;
		public PlayerActionAnimator ActionAnimator => _actionAnimator;
		public PlayerHandPeekController HandPeek => _handPeek;
		public PlayerPointController Point => _point;
		public PlayerVisual Visual => _visual;
		public ulong ClientId => OwnerClientId;

		// Folding is putting the cards down, and that is three things that must not come apart: the status
		// the rules read, the cards themselves, and the pose the player is holding them in. One method for
		// all three, because a hand mucked while its owner is still bent over studying it is a player
		// reading a hand they no longer have — and the two fold paths that existed before this each did
		// only the first part.
		public void ServerFold()
		{
			if (!IsServer || !_data || !_data.IsInHand) return;

			_data.ServerFold();

			// Dropped rather than left standing. The bool drives the animator on both rigs, so lowering it
			// is what the table watches the cards go face down.
			if (_handPeek) _handPeek.ServerSetPeeking(false);
		}

		// The seat number is the fallback rather than the label: a player whose identity RPC has not
		// landed yet still has a chair, and a name that arrives late replaces it on the next redraw.
		public string DisplayName
		{
			get
			{
				if (_wallet)
				{
					var name = _wallet.DisplayName.Value.ToString();
					if (!string.IsNullOrEmpty(name)) return name;
				}

				return _data && _data.IsSeated ? $"Seat {_data.SeatIndex.Value + 1}" : "Player";
			}
		}

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
			if (!_rig) _rig = GetComponent<PlayerRigController>();

			// Feature components live on child objects of the player rather than piling up on the root.
			if (!_headStretch) _headStretch = GetComponentInChildren<PlayerHeadStretchController>();
			if (!_actionAnimator) _actionAnimator = GetComponentInChildren<PlayerActionAnimator>();
			if (!_point) _point = GetComponentInChildren<PlayerPointController>();
			if (!_visual) _visual = GetComponentInChildren<PlayerVisual>();

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
