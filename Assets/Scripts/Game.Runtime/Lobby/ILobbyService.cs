using System;
using System.Collections.Generic;
using System.Threading;
using Unity.Netcode;
using UnityEngine;

namespace Game.Runtime.Lobby
{
	// Where rooms are opened, listed and joined. A failed create or join answers null rather than throwing,
	// so the caller reports it the same way whichever service it asked.
	public interface ILobbyService : IDisposable
	{
		string Name { get; }

		// Whether this service can be asked anything right now (Steam signed in, for instance).
		bool IsAvailable { get; }

		// The name the room is shown under in a list.
		string LocalUserName { get; }

		// A room the player agreed to join from outside the game, such as a friend's invite.
		event Action<ulong> OnInviteAccepted;

		void Initialize();

		Awaitable<ILobby> CreateAsync(LobbyCreateRequest request, CancellationToken ct = default);
		Awaitable<ILobby> JoinAsync(ulong lobbyId, CancellationToken ct = default);
		Awaitable<IReadOnlyList<ILobby>> SearchAsync(IReadOnlyList<LobbyData> filters, CancellationToken ct = default);

		void Leave(ILobby lobby);

		bool CanInvite(ILobby lobby);
		void OpenInviteDialog(ILobby lobby);

		// Points the transport this service connects through at the room, and hands it back to be used.
		NetworkTransport BindTransport(ILobby lobby, bool asHost);
	}
}
