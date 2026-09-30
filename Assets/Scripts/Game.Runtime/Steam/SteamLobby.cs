using Game.Runtime.Lobby;

namespace Game.Runtime.Steam
{
	public class SteamLobby : ILobby
	{
		public Steamworks.Data.Lobby Lobby { get; }

		public SteamLobby(Steamworks.Data.Lobby lobby)
		{
			Lobby = lobby;
		}

		public ulong Id => Lobby.Id.Value;
		public int MemberCount => Lobby.MemberCount;
		public int MaxMembers => Lobby.MaxMembers;

		public string GetData(string key) => Lobby.GetData(key);
	}
}
