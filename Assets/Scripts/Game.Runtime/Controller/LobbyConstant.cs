namespace Game.Runtime.Controller
{
	public static class LobbyConstant
	{
		public const string GameIDKey = "GameID";
		public const string GameIDValue = "GamblingGnomes";

		public const string RoomNameKey = "RoomName";
		public const string GameModeKey = "GameMode";

		// Whether matchmaking opened the room (1) or a player hosted it (0). Every room carries one or the
		// other, so a search can ask for either.
		public const string MatchmakingKey = "Matchmaking";
		public const string MatchmakingValue = "1";
		public const string HostedValue = "0";

		// Where a matchmaking room is: still gathering players, or its owner has opened the session.
		public const string MatchStateKey = "MatchState";
		public const string MatchStateGathering = "Gathering";
		public const string MatchStateStarted = "Started";
	}
}
