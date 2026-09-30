using System;
using System.Collections.Generic;
using System.Threading;
using Game.Runtime.Lobby;
using Steamworks;
using Steamworks.Data;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace Game.Runtime.Steam
{
	// Rooms on Steam's matchmaking, joined through Steam's relay. Invites come in through Steam too: the
	// overlay while running, or "+connect_lobby <id>" when the game was launched by accepting one.
	public class SteamLobbyService : ILobbyService
	{
		private readonly FacepunchTransport _relayTransport;
		private readonly UnityTransport _editorTransport;
		private readonly string _editorAddress;
		private readonly ushort _editorPort;

		// Hooking Steam's callbacks before SteamClient.Init loses them silently, so they are bound from the
		// initialised event as well as at once.
		private bool _bound;
		private bool _launchInviteRead;

		public event Action<ulong> OnInviteAccepted;

		// Two editors on one machine are signed into the same Steam account and cannot relay to themselves,
		// so in the editor the room is found on Steam but connected over the local transport, at the
		// address it was authored with.
		public SteamLobbyService(FacepunchTransport relayTransport, UnityTransport editorTransport)
		{
			_relayTransport = relayTransport;
			_editorTransport = editorTransport;

			if (_editorTransport)
			{
				_editorAddress = _editorTransport.ConnectionData.Address;
				_editorPort = _editorTransport.ConnectionData.Port;
			}
		}

		public string Name => "Steam";

		public bool IsAvailable => SteamController.IsInitialized && SteamClient.IsValid && SteamClient.IsLoggedOn;

		public string LocalUserName => SteamClient.IsValid ? SteamClient.Name : Environment.UserName;

		public void Initialize()
		{
			SteamController.OnInitialized += Bind;

			if (SteamController.IsInitialized) Bind();
		}

		public void Dispose()
		{
			SteamController.OnInitialized -= Bind;

			if (!_bound) return;
			_bound = false;

			SteamFriends.OnGameLobbyJoinRequested -= HandleJoinRequested;
			SteamMatchmaking.OnLobbyMemberLeave -= HandleMemberLeave;
			SteamMatchmaking.OnLobbyMemberJoined -= HandleMemberJoined;
		}

		private void Bind()
		{
			if (_bound) return;
			_bound = true;

			SteamMatchmaking.OnLobbyMemberJoined += HandleMemberJoined;
			SteamMatchmaking.OnLobbyMemberLeave += HandleMemberLeave;
			SteamFriends.OnGameLobbyJoinRequested += HandleJoinRequested;

			ReadLaunchInvite();
		}

		public async Awaitable<ILobby> CreateAsync(LobbyCreateRequest request, CancellationToken ct = default)
		{
			if (!IsAvailable) return null;

			// Steam cannot call a request already in flight back, so the token is only checked by the caller.
			var created = await SteamMatchmaking.CreateLobbyAsync(request.MaxMembers);
			if (!created.HasValue) return null;

			var lobby = created.Value;

			if (request.IsPrivate) lobby.SetPrivate();
			else lobby.SetPublic();

			lobby.SetJoinable(true);

			if (request.Data != null)
			{
				foreach (var pair in request.Data) lobby.SetData(pair.Key, pair.Value);
			}

			return new SteamLobby(lobby);
		}

		public async Awaitable<ILobby> JoinAsync(ulong lobbyId, CancellationToken ct = default)
		{
			if (!IsAvailable) return null;

			var joined = await SteamMatchmaking.JoinLobbyAsync(lobbyId);
			return joined.HasValue ? new SteamLobby(joined.Value) : null;
		}

		public async Awaitable<IReadOnlyList<ILobby>> SearchAsync(IReadOnlyList<LobbyData> filters, CancellationToken ct = default)
		{
			var found = new List<ILobby>();
			if (!IsAvailable) return found;

			var query = SteamMatchmaking.LobbyList;
			if (filters != null)
			{
				foreach (var filter in filters) query = query.WithKeyValue(filter.Key, filter.Value);
			}

			var lobbies = await query.RequestAsync();
			ct.ThrowIfCancellationRequested();

			if (lobbies == null) return found;

			foreach (var lobby in lobbies) found.Add(new SteamLobby(lobby));
			return found;
		}

		public void Leave(ILobby lobby)
		{
			// Steam can already be down: nothing orders one OnApplicationQuit against another, and calling into
			// a shut down client throws from inside Facepunch.
			if (lobby is SteamLobby steamLobby && SteamClient.IsValid) steamLobby.Lobby.Leave();
		}

		public bool CanInvite(ILobby lobby) => lobby is SteamLobby && SteamClient.IsValid;

		public void OpenInviteDialog(ILobby lobby)
		{
			if (!CanInvite(lobby))
			{
				Debug.LogWarning("[SteamLobbyService] Invite refused: not in a Steam lobby.");
				return;
			}

			// The overlay only exists when Steam could hook the process (a build launched through Steam, not the
			// editor); without it the call below does nothing at all, so say why.
			if (!SteamUtils.IsOverlayEnabled)
				Debug.LogWarning("[SteamLobbyService] Steam overlay is not available in this process, so the invite dialog cannot open. Launch the build through Steam.");

			SteamFriends.OpenGameInviteOverlay(lobby.Id);
		}

		public NetworkTransport BindTransport(ILobby lobby, bool asHost)
		{
			if (Application.isEditor && _editorTransport)
			{
				var data = _editorTransport.ConnectionData;
				data.Address = _editorAddress;
				data.Port = _editorPort;
				_editorTransport.ConnectionData = data;
				return _editorTransport;
			}

			_relayTransport.TargetSteamId = asHost || lobby is not SteamLobby steamLobby
				? SteamClient.SteamId.Value
				: steamLobby.Lobby.Owner.Id.Value;

			return _relayTransport;
		}

		private void HandleJoinRequested(Steamworks.Data.Lobby lobby, SteamId friendId) => OnInviteAccepted?.Invoke(lobby.Id.Value);

		private static void HandleMemberJoined(Steamworks.Data.Lobby lobby, Friend friend) => Debug.Log($"[SteamLobbyService] {friend.Name} joined lobby.");

		private static void HandleMemberLeave(Steamworks.Data.Lobby lobby, Friend friend) => Debug.Log($"[SteamLobbyService] {friend.Name} left lobby.");

		// A game started by accepting an invite while it was closed is launched by Steam with
		// "+connect_lobby <id>" on its command line. Read once, as soon as Steam can answer.
		private void ReadLaunchInvite()
		{
			if (_launchInviteRead) return;
			_launchInviteRead = true;

			var args = Environment.GetCommandLineArgs();
			for (var i = 0; i < args.Length - 1; i++)
			{
				if (args[i] != "+connect_lobby" || !ulong.TryParse(args[i + 1], out var id)) continue;

				Debug.Log($"[SteamLobbyService] Launched by an invite to lobby {id}.");
				OnInviteAccepted?.Invoke(id);
				return;
			}
		}
	}
}
