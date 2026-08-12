using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Game.Runtime.Player
{
	// Who the player is, as opposed to what their body is doing. The id is the one that outlives a
	// connection, so it is what a returning player would be matched on.
	public class PlayerData : NetworkBehaviour
	{
		[HideInInspector] public NetworkVariable<FixedString64Bytes> DisplayName = new(default,
			readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);

		[HideInInspector] public NetworkVariable<ulong> PlayerId = new(0,
			readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);

		public event Action OnIdentityChanged;

		public override void OnNetworkSpawn()
		{
			DisplayName.OnValueChanged += HandleNameChanged;
			PlayerId.OnValueChanged += HandleIdChanged;

			OnIdentityChanged?.Invoke();
		}

		public override void OnNetworkDespawn()
		{
			DisplayName.OnValueChanged -= HandleNameChanged;
			PlayerId.OnValueChanged -= HandleIdChanged;
		}

		public void ServerSetIdentity(ulong playerId, string displayName)
		{
			if (!IsServer) return;

			PlayerId.Value = playerId;

			// The name travels in a fixed buffer, so an overlong one is cut rather than allowed to throw
			// on the way out.
			var name = displayName ?? string.Empty;
			DisplayName.Value = name.Length > 32 ? name[..32] : name;
		}

		private void HandleNameChanged(FixedString64Bytes previous, FixedString64Bytes current) => OnIdentityChanged?.Invoke();
		private void HandleIdChanged(ulong previous, ulong current) => OnIdentityChanged?.Invoke();
	}
}
