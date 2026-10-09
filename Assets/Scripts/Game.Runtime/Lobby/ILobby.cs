namespace Game.Runtime.Lobby
{
	// A room players gather in before a session starts, as the service that listed it describes it.
	public interface ILobby
	{
		ulong Id { get; }
		int MemberCount { get; }
		int MaxMembers { get; }

		// The member the room answers to: who may change its data, and who hosts the session it opens.
		ulong OwnerId { get; }

		string GetData(string key);
	}
}
