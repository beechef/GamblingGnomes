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

		// This player as a member of a room, comparable with ILobby.OwnerId.
		ulong LocalMemberId { get; }

		// A room the player agreed to join from outside the game, such as a friend's invite.
		event Action<ulong> OnInviteAccepted;

		// A room this player is in gained or lost a member, changed owner or changed its data.
		event Action<ILobby> OnLobbyChanged;

		// A room this player is in can no longer be reached: it closed, or the service lost its connection.
		event Action<ILobby> OnLobbyLost;

		void Initialize();

		Awaitable<ILobby> CreateAsync(LobbyCreateRequest request, CancellationToken ct = default);
		Awaitable<ILobby> JoinAsync(ulong lobbyId, CancellationToken ct = default);
		Awaitable<IReadOnlyList<ILobby>> SearchAsync(IReadOnlyList<LobbyData> filters, CancellationToken ct = default);

		void Leave(ILobby lobby);

		// Owner only; ignored from anyone else.
		void SetData(ILobby lobby, string key, string value);
		void SetJoinable(ILobby lobby, bool joinable);

		// Hands the room to the member who has been in it longest besides the owner. False when the caller
		// does not own it or is alone in it.
		bool TransferOwnership(ILobby lobby);

		// Estimated round trip to the room's owner in milliseconds, or -1 when it cannot be told.
		int EstimatePing(ILobby lobby);

		bool CanInvite(ILobby lobby);
		void OpenInviteDialog(ILobby lobby);

		// Points the transport this service connects through at the room, and hands it back to be used.
		NetworkTransport BindTransport(ILobby lobby, bool asHost);
	}
}
