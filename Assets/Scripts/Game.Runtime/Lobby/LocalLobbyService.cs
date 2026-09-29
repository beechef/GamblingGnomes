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
	// every editor, Multiplayer Play Mode player and build on this machine lists the others. The host keeps
	// its file fresh; a file nobody has touched for a few seconds belongs to a host that crashed and is
	// swept away by the next search.
	public class LocalLobbyService : ILobbyService
	{
		private const string LoopbackAddress = "127.0.0.1";
		private const int BasePort = 7777;
		private const int PortRange = 64;
		private const float HeartbeatInterval = 1f;

		private static readonly TimeSpan StaleAfter = TimeSpan.FromSeconds(5);

		private readonly NetworkManager _network;
		private readonly UnityTransport _transport;
		private readonly string _directory;

		private LocalLobby _hosted;
		private CancellationTokenSource _heartbeat;

		public event Action<ulong> OnInviteAccepted
		{
			add { }
			remove { }
		}

		public LocalLobbyService(NetworkManager network, UnityTransport transport)
		{
			_network = network;
			_transport = transport;
			_directory = Path.Combine(Path.GetTempPath(), $"{Application.productName}_Lobbies");
		}

		public string Name => "Local";

		public bool IsAvailable => _transport;

		public string LocalUserName => Environment.UserName;

		public void Initialize()
		{
		}

		public void Dispose() => StopHosting();

		public Awaitable<ILobby> CreateAsync(LobbyCreateRequest request, CancellationToken ct = default)
		{
			StopHosting();

			var live = ReadLiveLobbies();
			var port = FreePort(live);
			if (port < 0) return Completed<ILobby>(null);

			var lobby = new LocalLobby(NewId(), LoopbackAddress, port, request);
			if (!Write(lobby)) return Completed<ILobby>(null);

			_hosted = lobby;
			_heartbeat = new CancellationTokenSource();
			_ = KeepAliveAsync(lobby, _heartbeat.Token);

			return Completed<ILobby>(lobby);
		}

		public Awaitable<ILobby> JoinAsync(ulong lobbyId, CancellationToken ct = default)
		{
			var lobby = Read(PathOf(lobbyId));
			var joinable = lobby != null && !lobby.IsStale(StaleAfter) && lobby.MemberCount < lobby.MaxMembers;

			return Completed<ILobby>(joinable ? lobby : null);
		}

		public Awaitable<IReadOnlyList<ILobby>> SearchAsync(IReadOnlyList<LobbyData> filters, CancellationToken ct = default)
		{
			var found = new List<ILobby>();

			foreach (var lobby in ReadLiveLobbies())
			{
				if (!lobby.IsPrivate && lobby.Matches(filters)) found.Add(lobby);
			}

			return Completed<IReadOnlyList<ILobby>>(found);
		}

		// Only the host's leaving closes the room; a client walking out has nothing to hand back.
		public void Leave(ILobby lobby)
		{
			if (lobby != null && _hosted != null && lobby.Id == _hosted.Id) StopHosting();
		}

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

		private async Awaitable KeepAliveAsync(LocalLobby lobby, CancellationToken ct)
		{
			try
			{
				while (!ct.IsCancellationRequested)
				{
					var members = _network && _network.IsServer ? _network.ConnectedClientsIds.Count : 1;
					lobby.Beat(members);
					Write(lobby);

					await AwaitableUtility.WaitUnscaledAsync(HeartbeatInterval, ct);
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

		private void StopHosting()
		{
			_heartbeat?.Cancel();
			_heartbeat?.Dispose();
			_heartbeat = null;

			if (_hosted == null) return;

			Delete(PathOf(_hosted.Id));
			_hosted = null;
		}

		private List<LocalLobby> ReadLiveLobbies()
		{
			var live = new List<LocalLobby>();
			if (!Directory.Exists(_directory)) return live;

			foreach (var path in Directory.GetFiles(_directory, "*.json"))
			{
				var lobby = Read(path);

				if (lobby == null || lobby.IsStale(StaleAfter))
				{
					Delete(path);
					continue;
				}

				live.Add(lobby);
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

		private string PathOf(ulong id) => Path.Combine(_directory, $"{id}.json");

		// Written aside and moved into place, so a search never reads half a file.
		private bool Write(LocalLobby lobby)
		{
			try
			{
				Directory.CreateDirectory(_directory);

				var path = PathOf(lobby.Id);
				var temp = path + ".tmp";
				File.WriteAllText(temp, JsonUtility.ToJson(lobby));

				if (File.Exists(path)) File.Replace(temp, path, null);
				else File.Move(temp, path);

				return true;
			}
			catch (IOException exception)
			{
				Debug.LogWarning($"[LocalLobbyService] Could not write the room file: {exception.Message}");
				return false;
			}
			catch (UnauthorizedAccessException exception)
			{
				Debug.LogWarning($"[LocalLobbyService] Could not write the room file: {exception.Message}");
				return false;
			}
		}

		private static LocalLobby Read(string path)
		{
			try
			{
				return File.Exists(path) ? JsonUtility.FromJson<LocalLobby>(File.ReadAllText(path)) : null;
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
