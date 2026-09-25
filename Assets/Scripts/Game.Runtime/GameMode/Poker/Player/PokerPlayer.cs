using System;
using System.Collections.Generic;
using Game.Runtime.GameMode.Poker.Hallucination;
using Game.Runtime.GameMode.Poker.Visual;
using Game.Runtime.Player;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Serialization;

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
		[FormerlySerializedAs("_items")]
		[SerializeField] private PokerBetItemConsumeController _betItemConsume;
		[SerializeField] private PokerBetItemCarryController _betItemCarry;
		[SerializeField] private PokerItemInventory _itemInventory;
		[SerializeField] private PokerItemKnowledge _itemKnowledge;
		[SerializeField] private PokerItemTargetingController _itemTargeting;
		[SerializeField] private PokerHandVisual _handVisual;
		[SerializeField] private PokerWinnerPoseController _winnerPose;
		[SerializeField] private PokerHallucinationRollController _hallucinationRoll;

		[Tooltip("Who this player is — the name the table shows. Lives beside this on the player, not on the table.")]
		[FormerlySerializedAs("_wallet")]
		[SerializeField] private PlayerData _identity;

		[Tooltip("The rig this client renders for this player — where an ability aiming at them finds a hand or a head.")]
		[SerializeField] private PlayerRigController _rig;

		[SerializeField] private PlayerActionAnimator _actionAnimator;

		[Tooltip("Puts a hand on what it is reaching for, so a gesture that ends near the right spot ends on it. Both rigs carry a constraint; this drives whichever one this client draws.")]
		[SerializeField] private PlayerHandIkController _handIk;

		[Tooltip("Who draws this player. Marking somebody out for the whole table is a change of how they are drawn, so it is asked of the thing already holding every renderer.")]
		[SerializeField] private PlayerVisual _visual;

		[Tooltip("The name over this player's head, lit up with the outline when somebody points at them.")]
		[SerializeField] private PlayerNameTagVisual _nameTag;

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

		// The record of what this player has swallowed. Its own controller rather than more verbs on the
		// data, and named for eating rather than for items in general: the cards they can choose to use are
		// ItemInventory's.
		public PokerBetItemConsumeController BetItemConsume => _betItemConsume;

		// Carrying a staked cap through the bet gesture. On the player because which rig is drawn, where a
		// fist closes and which frame the hand arrives on are all this body's own business.
		public PokerBetItemCarryController BetItemCarry => _betItemCarry;

		// The item cards held, and what they have told this player. Empty on a table that plays without items.
		public PokerItemInventory ItemInventory => _itemInventory;
		public PokerItemKnowledge ItemKnowledge => _itemKnowledge;
		public PokerItemTargetingController ItemTargeting => _itemTargeting;

		// The cards this player holds, as they lie on this screen.
		public PokerHandVisual HandVisual => _handVisual;

		// The held celebration. Named as a pose rather than as a gesture, because it lasts as long as the
		// round says and not as long as a clip.
		public PokerWinnerPoseController WinnerPose => _winnerPose;
		public PokerHallucinationRollController HallucinationRoll => _hallucinationRoll;
		public PlayerData Identity => _identity;
		public PlayerRigController Rig => _rig;
		public PlayerActionAnimator ActionAnimator => _actionAnimator;
		public PlayerHandIkController HandIk => _handIk;
		public PlayerVisual Visual => _visual;
		public PlayerNameTagVisual NameTag => _nameTag;
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

			// Every fold is seen being thrown in, a timeout or a caught cheat as much as a button press.
			if (_actionAnimator) _actionAnimator.ServerPlay(PlayerActionIds.Fold);
		}

		// The seat number is the fallback rather than the label: a player whose identity RPC has not
		// landed yet still has a chair, and a name that arrives late replaces it on the next redraw.
		public string DisplayName
		{
			get
			{
				if (_identity)
				{
					var name = _identity.DisplayName.Value.ToString();
					if (!string.IsNullOrEmpty(name)) return name;
				}

				return _data && _data.IsSeated ? $"Seat {_data.SeatIndex.Value + 1}" : "Player";
			}
		}

		// The name to print for a client, whether or not their body is still here.
		public static string NameOf(ulong clientId)
		{
			var player = Find(clientId);
			return player ? player.DisplayName : $"Player {clientId}";
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
			if (!_betItemConsume) _betItemConsume = GetComponentInChildren<PokerBetItemConsumeController>(true);
			if (!_betItemCarry) _betItemCarry = GetComponentInChildren<PokerBetItemCarryController>(true);
			if (!_winnerPose) _winnerPose = GetComponentInChildren<PokerWinnerPoseController>(true);
			if (!_hallucinationRoll) _hallucinationRoll = GetComponentInChildren<PokerHallucinationRollController>(true);
			if (!_identity) _identity = GetComponent<PlayerData>();
			if (!_rig) _rig = GetComponent<PlayerRigController>();

			// Feature components live on child objects of the player rather than piling up on the root.
			if (!_actionAnimator) _actionAnimator = GetComponentInChildren<PlayerActionAnimator>();
			if (!_handIk) _handIk = GetComponentInChildren<PlayerHandIkController>(true);
			if (!_visual) _visual = GetComponentInChildren<PlayerVisual>();
			if (!_nameTag) _nameTag = GetComponentInChildren<PlayerNameTagVisual>(true);
			if (!_handVisual) _handVisual = GetComponentInChildren<PokerHandVisual>(true);
			if (!_itemInventory) _itemInventory = GetComponentInChildren<PokerItemInventory>(true);
			if (!_itemKnowledge) _itemKnowledge = GetComponentInChildren<PokerItemKnowledge>(true);
			if (!_itemTargeting) _itemTargeting = GetComponentInChildren<PokerItemTargetingController>(true);

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
