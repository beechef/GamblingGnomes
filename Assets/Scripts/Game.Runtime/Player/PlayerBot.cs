namespace Game.Runtime.Player
{
	// A body with no connection behind it, owned by a client id Netcode never hands out. Everything that
	// would wait on that owner (moving the body, sitting it down) is done by the server instead.
	public static class PlayerBot
	{
		public const ulong FirstClientId = 1UL << 40;

		public static bool IsBot(ulong clientId) => clientId >= FirstClientId && clientId != ulong.MaxValue;
	}
}
