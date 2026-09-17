using UnityEngine;

namespace Game.Runtime.Props
{
	// Marks a renderer that belongs to a prop without being part of how the prop looks — a hover outline,
	// a selection ring. Whatever repaints a prop's renderers skips it, or a hallucination painting the card
	// would paint the card's picture onto the outline around it too.
	[RequireComponent(typeof(Renderer))]
	public class PropPaintIgnore : MonoBehaviour
	{
	}
}
