namespace Game.Runtime.Lobby
{
	public readonly struct LobbyJoinResult
	{
		public readonly ILobby Lobby;
		public readonly ConnectFailure Failure;

		private LobbyJoinResult(ILobby lobby, ConnectFailure failure)
		{
			Lobby = lobby;
			Failure = failure;
		}

		public static LobbyJoinResult Joined(ILobby lobby) => new(lobby, ConnectFailure.Unknown);

		public static LobbyJoinResult Failed(ConnectFailure failure) => new(null, failure);
	}
}
