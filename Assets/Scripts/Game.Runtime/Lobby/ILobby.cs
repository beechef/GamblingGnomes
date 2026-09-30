namespace Game.Runtime.Lobby
{
	// A room players gather in before a session starts, as the service that listed it describes it.
	public interface ILobby
	{
		ulong Id { get; }
		int MemberCount { get; }
		int MaxMembers { get; }

		string GetData(string key);
	}
}
