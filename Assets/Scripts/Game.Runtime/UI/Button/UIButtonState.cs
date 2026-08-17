namespace Game.Runtime.UI.Button
{
	// Resolved in priority order — Disabled > Pressed > Selected > Hovered > Normal. A disabled button
	// is never hovered, a held one is never merely hovered, and the entry a menu is sitting on stays
	// marked even when the mouse wanders off it. Anything drawing a button reads this rather than
	// tracking the pointer itself.
	public enum UIButtonState : byte
	{
		Normal = 0,
		Hovered = 1,
		Selected = 2,
		Pressed = 3,
		Disabled = 4
	}
}
