using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Game.Runtime.Utility;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace Game.Runtime.Lobby
{
	// Rooms that need neither Steam nor the internet: each one is a file in the machine's temp folder, so
	// every editor, Multiplayer Play Mode player and build on this machine lists the others.
	//
	// The room file is written only by its owner. Each member keeps a file of its own fresh beside it, and
	// the owner counts the live ones into the room. Every beat a member also reads the room back, which is
	// how it hears of a new owner, new data or the room closing. A file nobody has touched for a few seconds
	// belongs to a process that died: a dead owner's room is lost to everyone in it and swept by the next
	// search, a dead member drops out of the count.
	public class LocalLobbyService : ILobbyService
	{
		private const string LoopbackAddress = "127.0.0.1";
		private const int BasePort = 7777;
		private const int PortRange = 64;
		private const float HeartbeatInterval = 1f;
		private const string RoomExtension = ".json";
		private const string MemberExtension = ".member";

		private static readonly TimeSpan StaleAfter = TimeSpan.FromSeconds(5);

		private readonly UnityTransport _transport;
		private readonly string _directory;
		private readonly ulong _localId = NewId();

		private LocalLobby _current;
		private LocalLobbyMember _membership;
		private string _lastSignature;
		private CancellationTokenSource _heartbeat;

		public event Action<ulong> OnInviteAccepted
		{
			add { }
			remove { }
		}

		public event Action<ILobby> OnLobbyChanged;
		public event Action<ILobby> OnLobbyLost;

		public LocalLobbyService(UnityTransport transport)
		{
			_transport = transport;
			_directory = Path.Combine(Path.GetTempPath(), $"{Application.productName}_Lobbies");
		}

		public string Name => "Local";

		public bool IsAvailable => _transport;

		public string LocalUserName => Environment.UserName;

		public ulong LocalMemberId => _localId;

		public void Initialize()
		{
		}

		public void Dispose() => LeaveCurrent();

		public Awaitable<ILobby> CreateAsync(LobbyCreateRequest request, CancellationToken ct = default)
		{
			LeaveCurrent();

			var live = ReadLiveLobbies();
			var port = FreePort(live);
			if (port < 0) return Completed<ILobby>(null);

			var lobby = new LocalLobby(NewId(), _localId, LoopbackAddress, port, request);
			lobby.Beat(1);
			if (!Write(RoomPath(lobby.Id), lobby)) return Completed<ILobby>(null);

			Enter(lobby);
			return Completed<ILobby>(lobby);
		}

		public Awaitable<ILobby> JoinAsync(ulong lobbyId, CancellationToken ct = default)
		{
			var lobby = Read<LocalLobby>(RoomPath(lobbyId));
			var joinable = lobby != null && !lobby.IsStale(StaleAfter) && lobby.IsJoinable && lobby.MemberCount < lobby.MaxMembers;
			if (!joinable) return Completed<ILobby>(null);

			LeaveCurrent();
			Enter(lobby);
			return Completed<ILobby>(lobby);
		}

		public Awaitable<IReadOnlyList<ILobby>> SearchAsync(IReadOnlyList<LobbyData> filters, CancellationToken ct = default)
		{
			var found = new List<ILobby>();

			foreach (var lobby in ReadLiveLobbies())
			{
				if (!lobby.IsPrivate && lobby.IsJoinable && lobby.Matches(filters)) found.Add(lobby);
			}

			return Completed<IReadOnlyList<ILobby>>(found);
		}

		// An owner walking out closes the room; hand it on first with TransferOwnership to keep it open.
		public void Leave(ILobby lobby)
		{
			if (lobby != null && _current != null && lobby.Id == _current.Id) LeaveCurrent();
		}

		public void SetData(ILobby lobby, string key, string value)
		{
			if (!IsOwnedHere(lobby)) return;

			_current.SetData(key, value);
			WriteRoom();
		}

		public void SetJoinable(ILobby lobby, bool joinable)
		{
			if (!IsOwnedHere(lobby)) return;

			_current.SetJoinable(joinable);
			WriteRoom();
		}

		public bool TransferOwnership(ILobby lobby)
		{
			if (!IsOwnedHere(lobby)) return false;

			LocalLobbyMember successor = null;
			foreach (var member in ReadLiveMembers(_current.Id))
			{
				if (member.MemberId == _localId) continue;
				if (successor == null || member.JoinedTicks < successor.JoinedTicks) successor = member;
			}

			if (successor == null) return false;

			_current.SetOwner(successor.MemberId);
			WriteRoom();
			return true;
		}

		// Every local room is on this machine.
		public int EstimatePing(ILobby lobby) => lobby is LocalLobby ? 0 : -1;

		public bool CanInvite(ILobby lobby) => false;

		public void OpenInviteDialog(ILobby lobby) =>
			Debug.LogWarning("[LocalLobbyService] Local rooms have no invites; the other player finds the room in the lobby list.");

		public NetworkTransport BindTransport(ILobby lobby, bool asHost)
		{
			if (lobby is LocalLobby local)
			{
				var data = _transport.ConnectionData;
				data.Address = local.Address;
				data.Port = (ushort)local.Port;
				data.ServerListenAddress = local.Address;
				_transport.ConnectionData = data;
			}

			return _transport;
		}

		private bool IsOwnedHere(ILobby lobby) => _current != null && lobby != null && lobby.Id == _current.Id && _current.OwnerId == _localId;

		private void Enter(LocalLobby lobby)
		{
			_current = lobby;
			_lastSignature = lobby.Signature();

			_membership = new LocalLobbyMember(_localId);
			_membership.Beat();
			Write(MemberPath(lobby.Id, _localId), _membership);

			_heartbeat = new CancellationTokenSource();
			_ = HeartbeatAsync(_heartbeat.Token);
		}

		private void LeaveCurrent()
		{
			_heartbeat?.Cancel();
			_heartbeat?.Dispose();
			_heartbeat = null;

			if (_current == null) return;

			Delete(MemberPath(_current.Id, _localId));
			if (_current.OwnerId == _localId) Delete(RoomPath(_current.Id));

			_current = null;
			_membership = null;
		}

		private async Awaitable HeartbeatAsync(CancellationToken ct)
		{
			try
			{
				while (!ct.IsCancellationRequested)
				{
					await AwaitableUtility.WaitUnscaledAsync(HeartbeatInterval, ct);
					Beat();
				}
			}
			catch (OperationCanceledException)
			{
			}
			catch (Exception exception)
			{
				Debug.LogException(exception);
			}
		}

		private void Beat()
		{
			if (_current == null) return;

			_membership.Beat();
			Write(MemberPath(_current.Id, _localId), _membership);

			var written = Read<LocalLobby>(RoomPath(_current.Id));

			// Gone or gone quiet: the owner closed it or died. Either way nobody is left to host it.
			if (written == null || (written.OwnerId != _localId && written.IsStale(StaleAfter)))
			{
				var lost = _current;
				LeaveCurrent();
				OnLobbyLost?.Invoke(lost);
				return;
			}

			if (written.OwnerId == _localId)
			{
				// Taken over from the file rather than the copy in hand, so a room handed to this member
				// arrives with everything the last owner wrote.
				_current.CopyFrom(written);
				_current.Beat(ReadLiveMembers(_current.Id).Count);
				WriteRoom();
			}
			else
			{
				_current.CopyFrom(written);
			}

			var signature = _current.Signature();
			if (signature == _lastSignature) return;

			_lastSignature = signature;
			OnLobbyChanged?.Invoke(_current);
		}

		private void WriteRoom()
		{
			if (_current != null) Write(RoomPath(_current.Id), _current);
		}

		private List<LocalLobby> ReadLiveLobbies()
		{
			var live = new List<LocalLobby>();
			if (!Directory.Exists(_directory)) return live;

			foreach (var path in Directory.GetFiles(_directory, "*" + RoomExtension))
			{
				var lobby = Read<LocalLobby>(path);

				if (lobby == null || lobby.IsStale(StaleAfter))
				{
					Delete(path);
					continue;
				}

				live.Add(lobby);
			}

			// Members of a room that is gone have nobody left to sweep them.
			foreach (var path in Directory.GetFiles(_directory, "*" + MemberExtension))
			{
				var member = Read<LocalLobbyMember>(path);
				if (member == null || member.IsStale(StaleAfter)) Delete(path);
			}

			return live;
		}

		private List<LocalLobbyMember> ReadLiveMembers(ulong lobbyId)
		{
			var live = new List<LocalLobbyMember>();
			if (!Directory.Exists(_directory)) return live;

			foreach (var path in Directory.GetFiles(_directory, $"{lobbyId}.*{MemberExtension}"))
			{
				var member = Read<LocalLobbyMember>(path);

				if (member == null || member.IsStale(StaleAfter))
				{
					Delete(path);
					continue;
				}

				live.Add(member);
			}

			return live;
		}

		private static int FreePort(List<LocalLobby> live)
		{
			for (var port = BasePort; port < BasePort + PortRange; port++)
			{
				if (live.TrueForAll(lobby => lobby.Port != port)) return port;
			}

			Debug.LogWarning($"[LocalLobbyService] Every port from {BasePort} to {BasePort + PortRange - 1} is taken by a live room.");
			return -1;
		}

		private static ulong NewId()
		{
			var bytes = Guid.NewGuid().ToByteArray();
			var id = BitConverter.ToUInt64(bytes, 0);
			return id == 0 ? 1 : id;
		}

		private string RoomPath(ulong id) => Path.Combine(_directory, id + RoomExtension);

		private string MemberPath(ulong lobbyId, ulong memberId) => Path.Combine(_directory, $"{lobbyId}.{memberId}{MemberExtension}");

		// Written aside and moved into place, so a reader never sees half a file.
		private bool Write(string path, object value)
		{
			try
			{
				Directory.CreateDirectory(_directory);

				var temp = path + ".tmp";
				File.WriteAllText(temp, JsonUtility.ToJson(value));

				if (File.Exists(path)) File.Replace(temp, path, null);
				else File.Move(temp, path);

				return true;
			}
			catch (IOException exception)
			{
				Debug.LogWarning($"[LocalLobbyService] Could not write {Path.GetFileName(path)}: {exception.Message}");
				return false;
			}
			catch (UnauthorizedAccessException exception)
			{
				Debug.LogWarning($"[LocalLobbyService] Could not write {Path.GetFileName(path)}: {exception.Message}");
				return false;
			}
		}

		private static T Read<T>(string path) where T : class
		{
			try
			{
				return File.Exists(path) ? JsonUtility.FromJson<T>(File.ReadAllText(path)) : null;
			}
			catch (Exception)
			{
				// Another process is mid-write or the file is not ours; it is treated as absent.
				return null;
			}
		}

		private static void Delete(string path)
		{
			try
			{
				if (File.Exists(path)) File.Delete(path);
			}
			catch (IOException)
			{
			}
			catch (UnauthorizedAccessException)
			{
			}
		}

		private static Awaitable<T> Completed<T>(T value)
		{
			var source = new AwaitableCompletionSource<T>();
			source.SetResult(value);
			return source.Awaitable;
		}
	}
}
