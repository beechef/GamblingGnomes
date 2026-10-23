namespace Game.Runtime.Lobby
{
	// Why a way into a room did not end at the table, or why the table was lost.
	public enum ConnectFailure
	{
		Unknown,
		RoomFull,
		Timeout,
		RoomGone,
		Unavailable,
		Disconnected,
		Kicked
	}
}
