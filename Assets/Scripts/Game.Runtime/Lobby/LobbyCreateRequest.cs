using System.Collections.Generic;

namespace Game.Runtime.Lobby
{
	public readonly struct LobbyCreateRequest
	{
		public readonly int MaxMembers;
		public readonly bool IsPrivate;
		public readonly IReadOnlyList<LobbyData> Data;

		public LobbyCreateRequest(int maxMembers, bool isPrivate, IReadOnlyList<LobbyData> data)
		{
			MaxMembers = maxMembers;
			IsPrivate = isPrivate;
			Data = data;
		}
	}
}
