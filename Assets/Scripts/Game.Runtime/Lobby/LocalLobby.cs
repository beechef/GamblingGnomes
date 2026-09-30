using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Runtime.Lobby
{
	// One room of the local lobby, as written to its file: where the host listens and what it advertises.
	[Serializable]
	public class LocalLobby : ILobby
	{
		[SerializeField] private string _id;
		[SerializeField] private string _address;
		[SerializeField] private int _port;
		[SerializeField] private int _maxMembers;
		[SerializeField] private int _memberCount;
		[SerializeField] private bool _isPrivate;
		[SerializeField] private long _heartbeatTicks;
		[SerializeField] private List<LobbyData> _data = new();

		public LocalLobby()
		{
		}

		public LocalLobby(ulong id, string address, int port, LobbyCreateRequest request)
		{
			_id = id.ToString();
			_address = address;
			_port = port;
			_maxMembers = request.MaxMembers;
			_memberCount = 1;
			_isPrivate = request.IsPrivate;

			if (request.Data != null) _data.AddRange(request.Data);
		}

		public ulong Id => ulong.TryParse(_id, out var id) ? id : 0;
		public int MemberCount => _memberCount;
		public int MaxMembers => _maxMembers;

		public string Address => _address;
		public int Port => _port;
		public bool IsPrivate => _isPrivate;

		public string GetData(string key)
		{
			foreach (var pair in _data)
			{
				if (pair.Key == key) return pair.Value;
			}

			return string.Empty;
		}

		public bool Matches(IReadOnlyList<LobbyData> filters)
		{
			if (filters == null) return true;

			foreach (var filter in filters)
			{
				if (GetData(filter.Key) != filter.Value) return false;
			}

			return true;
		}

		public bool IsStale(TimeSpan after) => DateTime.UtcNow.Ticks - _heartbeatTicks > after.Ticks;

		public void Beat(int memberCount)
		{
			_memberCount = memberCount;
			_heartbeatTicks = DateTime.UtcNow.Ticks;
		}
	}
}
