namespace Game.Runtime.Props
{
	// The looks a prop is allowed to be switched to. An enum rather than a string id because both halves
	// have to name the same one and only one of them is in the prefab: two dropdowns cannot disagree
	// where two typed ids silently never match.
	public enum PropVariant
	{
		Default,
		Smiling,
		Melting,
		Watching
	}
}
