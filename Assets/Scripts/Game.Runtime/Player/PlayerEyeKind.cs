namespace Game.Runtime.Player
{
	// Which eyes a body is drawn with, apart from its version and outfit. Each model answers with its own
	// eye mask (PlayerModel.EyeMaskFor). Serialized as an int, never renumbered.
	public enum PlayerEyeKind
	{
		Default = 0,
		Anime = 1
	}
}
