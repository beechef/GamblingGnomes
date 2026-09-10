namespace Game.Runtime.Props
{
	// How a material takes over a renderer. Two genuinely different operations rather than a preference,
	// and which one is right is the shape of the effect: a pass drawn on top cannot bend what is underneath
	// it, and a replacement cannot leave anything readable through it.
	public enum PropPaintMode
	{
		// Every slot on the renderer becomes this material. What the thing looked like is gone while it
		// runs — the right answer for something that has to read as made of something else.
		Replace = 0,

		// Hung on the end of what the renderer is already wearing, as an extra pass. A card keeps its face
		// and the material draws over it, which is the only way a rank stays readable underneath.
		Add = 1
	}
}
