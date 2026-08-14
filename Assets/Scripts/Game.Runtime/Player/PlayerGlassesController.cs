using Unity.Netcode;
using UnityEngine;

namespace Game.Runtime.Player
{
	// A pair of glasses and nothing more: the wear everyone sees, and a number only the wearer's client is
	// told. What that number buys — board cards in poker, something else in another mode — is whatever
	// rule a mode installs against it; this component neither knows nor cares, which is what lets any
	// mode put glasses on a player.
	public class PlayerGlassesController : NetworkBehaviour
	{
		[Header("Animation")]
		[SerializeField] private string _wearActionId = PlayerActionIds.WearGlasses;
		[SerializeField] private string _removeActionId = PlayerActionIds.RemoveGlasses;

		[Header("Prop")]
		[Tooltip("Glasses meshes shown while the wear is on — one per rig, so the owner sees their own pair too. Optional until the art lands.")]
		[SerializeField] private GameObject[] _glassesVisuals;

		[Header("References")]
		[SerializeField] private PlayerActionAnimator _actionAnimator;

		// The public half: the act itself, replicated so every table watches the same gesture and prop for
		// exactly as long as the wear lasts.
		[HideInInspector] public NetworkVariable<bool> IsWorn = new(false,
			readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);

		// The private half: what the wear secretly grants its owner. Zero on a pair that is only a prop —
		// which is the whole bluff — and the meaning of any other number belongs to the mode that set it.
		[HideInInspector] public NetworkVariable<int> OwnerGrant = new(0,
			readPerm: NetworkVariableReadPermission.Owner, writePerm: NetworkVariableWritePermission.Server);

		private double _releaseTime;
		private bool _serverWearing;

		public override void OnNetworkSpawn()
		{
			// Feature components sit on child objects of the player, so the search starts from the network
			// root rather than assuming everything shares one GameObject.
			if (!_actionAnimator) _actionAnimator = NetworkObject.GetComponentInChildren<PlayerActionAnimator>();

			IsWorn.OnValueChanged += HandleWornChanged;

			// A client joining mid-wear reads the state as it stands rather than waiting for it to change.
			ApplyProp(IsWorn.Value);
		}

		public override void OnNetworkDespawn()
		{
			IsWorn.OnValueChanged -= HandleWornChanged;

			_serverWearing = false;
		}

		public void ServerWear(float duration, int grant)
		{
			if (!IsServer) return;

			OwnerGrant.Value = Mathf.Max(0, grant);
			IsWorn.Value = true;

			_releaseTime = NetworkManager.ServerTime.Time + Mathf.Max(0f, duration);
			_serverWearing = true;

			if (_actionAnimator) _actionAnimator.ServerPlay(_wearActionId);
		}

		public void ServerRemove()
		{
			if (!IsServer || !IsWorn.Value) return;

			_serverWearing = false;
			IsWorn.Value = false;
			OwnerGrant.Value = 0;

			if (_actionAnimator) _actionAnimator.ServerPlay(_removeActionId);
		}

		// Not a poll for a dependency: the wear ends on a clock, and somebody has to notice the moment
		// it does.
		private void Update()
		{
			if (!IsServer || !IsSpawned || !_serverWearing) return;
			if (NetworkManager.ServerTime.Time < _releaseTime) return;

			ServerRemove();
		}

		private void HandleWornChanged(bool previous, bool current) => ApplyProp(current);

		private void ApplyProp(bool worn)
		{
			foreach (var visual in _glassesVisuals)
			{
				if (visual && visual.activeSelf != worn) visual.SetActive(worn);
			}
		}
	}
}
