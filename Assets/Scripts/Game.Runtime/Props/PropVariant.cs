namespace Game.Runtime.Props
{
	// The looks a prop is allowed to be switched to. An enum rather than a string id because both halves
	// have to name the same one and only one of them is in the prefab: two dropdowns cannot disagree
	// where two typed ids silently never match.
	//
	// A name says what the prop becomes, never which effect asked — the same look is worth reaching for
	// from anywhere, and a variant called "Hallucination3" would be a rung number written into the art.
	public enum PropVariant
	{
		Default,
		// Value 1 is serialized in every look and effect that names it — renamed from Smiling, never renumbered.
		Transformed = 1,
		ExtraArm,
		Breasts
	}
}
