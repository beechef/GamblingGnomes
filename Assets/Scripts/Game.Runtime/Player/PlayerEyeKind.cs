namespace Game.Runtime.Player
{
	// Which eyes a body is drawn with. Each model answers with its own face material per version
	// (PlayerModel.FaceFor); a model with no answer keeps its own eyes. Serialized as an int, never renumbered.
	public enum PlayerEyeKind
	{
		Default = 0,
		Anime = 1
	}
}
