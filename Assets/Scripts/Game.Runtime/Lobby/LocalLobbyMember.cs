using System;
using UnityEngine;

namespace Game.Runtime.Lobby
{
	// One member's presence in a local room, kept fresh by the member itself in a file of its own, so nobody
	// ever writes over somebody else's.
	[Serializable]
	public class LocalLobbyMember
	{
		[SerializeField] private string _memberId;
		[SerializeField] private long _joinedTicks;
		[SerializeField] private long _heartbeatTicks;

		public LocalLobbyMember()
		{
		}

		public LocalLobbyMember(ulong memberId)
		{
			_memberId = memberId.ToString();
			_joinedTicks = DateTime.UtcNow.Ticks;
		}

		public ulong MemberId => ulong.TryParse(_memberId, out var id) ? id : 0;
		public long JoinedTicks => _joinedTicks;

		public bool IsStale(TimeSpan after) => DateTime.UtcNow.Ticks - _heartbeatTicks > after.Ticks;

		public void Beat() => _heartbeatTicks = DateTime.UtcNow.Ticks;
	}
}
