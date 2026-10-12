namespace Game.Runtime.Voice
{
	// Another player in the voice channel. The id is their Unity Authentication player id, stable across
	// sessions, so per-player volumes are saved under it.
	public readonly struct VoicePeer
	{
		public readonly string Id;
		public readonly string DisplayName;

		public VoicePeer(string id, string displayName)
		{
			Id = id;
			DisplayName = displayName;
		}
	}
}
