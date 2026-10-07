namespace Game.Runtime.Controller
{
	public static class LobbyConstant
	{
		public const string GameIDKey = "GameID";
		public const string GameIDValue = "GamblingGnomes";

		public const string RoomNameKey = "RoomName";
		public const string GameModeKey = "GameMode";

		// A room opened by matchmaking rather than by a player hosting one.
		public const string MatchmakingKey = "Matchmaking";
		public const string MatchmakingValue = "1";

		// Where a matchmaking room is: still gathering players, or its owner has opened the session.
		public const string MatchStateKey = "MatchState";
		public const string MatchStateGathering = "Gathering";
		public const string MatchStateStarted = "Started";
	}
}
