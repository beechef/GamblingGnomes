namespace Game.Runtime.Player
{
	// How one modifier joins the others on a bone.
	public enum PlayerBoneScaleMode : byte
	{
		// Scales whatever the rest of them arrived at. Two effects each asking for 2 give 4, and one asking
		// for 2 beside one asking for 0.5 gives the bone back at its authored size — which is the whole
		// point of them composing rather than one of them winning.
		Multiply = 0,

		// A flat amount on top, after every multiply. For something that wants to add a fixed thickness
		// whatever else is happening to the bone.
		Add = 1
	}
}
