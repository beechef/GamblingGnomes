namespace Game.Runtime.Player
{
	// Which version of a model and its outfit is drawn. Every model authors its own versions, and one that
	// has no look for the version asked is drawn in Cartoon — what the art ships wearing and what a body
	// wears whenever nothing asks otherwise. Named after the look, never after whatever asked for it.
	public enum PlayerLookVersion
	{
		Cartoon,
		Realistic
	}
}
