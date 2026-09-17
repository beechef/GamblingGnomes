namespace Game.Runtime.UI.CursorVisuals
{
	// What the pointer looks like. Named after the look, never after the moment asking for it, so a second
	// moment wanting a skull asks for the same one.
	//
	// The order is the priority, and nothing else checks it: when several are asked for at once the highest
	// value is drawn, so a look a whole beat asks for (Skull) is never replaced by the pointer passing over a
	// button (Interact). Default is what is drawn with nothing asked for. Append new looks where their rank
	// belongs, and read every request before renumbering — the values are not serialized anywhere but the
	// CursorVisual children, which name their state by value.
	public enum CursorVisualState
	{
		Default = 0,
		Interact = 10,
		Skull = 20
	}
}
