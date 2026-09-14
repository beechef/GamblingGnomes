using System;
using Steamworks;
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

		// Which of the table's colours this player is wearing. An index rather than the colour itself, so
		// the palette is an asset that can be restyled without touching the network — and so a client can
		// never be looking at a colour the others are not. Handed out by whoever owns the pool of them;
		// negative until it has been.
		[HideInInspector] public NetworkVariable<int> ColorIndex = new(-1,
			readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);

		public event Action OnIdentityChanged;

		public override void OnNetworkSpawn()
		{
			DisplayName.OnValueChanged += HandleNameChanged;
			PlayerId.OnValueChanged += HandleIdChanged;

			// Steam only knows whoever is signed in at this machine, so each player reports their own
			// name and id. The server cannot ask on their behalf: the transport hands it a bare number,
			// and the lobby it would look the name up in may not list a member who only just arrived.
			if (IsOwner) ReportIdentity();

			OnIdentityChanged?.Invoke();
		}

		private void ReportIdentity()
		{
			var playerId = SteamClient.IsValid ? SteamClient.SteamId.Value : OwnerClientId;
			var displayName = SteamClient.IsValid ? SteamClient.Name : $"Player {OwnerClientId}";

			// Two editor instances are signed into the same Steam account and would otherwise be one
			// player as far as the table is concerned. Each draws its own number, so they pull apart
			// without anything having to hand out ids centrally.
			if (Application.isEditor)
			{
				var suffix = UnityEngine.Random.Range(1000, 10000);

				playerId ^= (ulong)suffix << 32;
				displayName = $"{displayName}_{suffix}";
			}

			SubmitIdentityRPC(playerId, displayName);
		}

		// Taken on trust: Steam identity lives on the client, so there is nothing on the server to check
		// it against. A modified client can claim any name, which is worth knowing before this is used
		// for anything but display and matching a returning player to their own body.
		[Rpc(SendTo.Server)]
		private void SubmitIdentityRPC(ulong playerId, string displayName)
		{
			ServerSetIdentity(playerId, displayName);
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

		public void ServerSetColorIndex(int index)
		{
			if (!IsServer) return;

			ColorIndex.Value = index;
		}

		private void HandleNameChanged(FixedString64Bytes previous, FixedString64Bytes current) => OnIdentityChanged?.Invoke();
		private void HandleIdChanged(ulong previous, ulong current) => OnIdentityChanged?.Invoke();
	}
}
