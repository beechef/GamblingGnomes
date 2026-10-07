using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Runtime.Lobby
{
	// One room of the local lobby, as written to its file by its owner: where the host listens, who owns it,
	// how many are in it and what it advertises.
	[Serializable]
	public class LocalLobby : ILobby
	{
		[SerializeField] private string _id;
		[SerializeField] private string _ownerId;
		[SerializeField] private string _address;
		[SerializeField] private int _port;
		[SerializeField] private int _maxMembers;
		[SerializeField] private int _memberCount;
		[SerializeField] private bool _isPrivate;
		[SerializeField] private bool _joinable = true;
		[SerializeField] private long _heartbeatTicks;
		[SerializeField] private List<LobbyData> _data = new();

		public LocalLobby()
		{
		}

		public LocalLobby(ulong id, ulong ownerId, string address, int port, LobbyCreateRequest request)
		{
			_id = id.ToString();
			_ownerId = ownerId.ToString();
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
		public ulong OwnerId => ulong.TryParse(_ownerId, out var id) ? id : 0;

		public string Address => _address;
		public int Port => _port;
		public bool IsPrivate => _isPrivate;
		public bool IsJoinable => _joinable;

		public string GetData(string key)
		{
			foreach (var pair in _data)
			{
				if (pair.Key == key) return pair.Value;
			}

			return string.Empty;
		}

		public void SetData(string key, string value)
		{
			for (var i = 0; i < _data.Count; i++)
			{
				if (_data[i].Key != key) continue;

				_data[i] = new LobbyData(key, value);
				return;
			}

			_data.Add(new LobbyData(key, value));
		}

		public void SetJoinable(bool joinable) => _joinable = joinable;

		public void SetOwner(ulong ownerId) => _ownerId = ownerId.ToString();

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

		// What a member who does not own the room sees of it: everything the owner last wrote.
		public void CopyFrom(LocalLobby written) => JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(written), this);

		// Changes worth telling a member about; the heartbeat moves every beat and is left out.
		public string Signature() => $"{_ownerId}|{_memberCount}|{_joinable}|{JsonUtility.ToJson(new DataList { Items = _data })}";

		[Serializable]
		private struct DataList
		{
			public List<LobbyData> Items;
		}
	}
}
