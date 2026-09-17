using UnityEngine;

namespace Game.Runtime.UI.CursorVisuals
{
	// One look the pointer can take, as its own object: the controller switches it on while its state is
	// wanted and moves it with the pointer. The tip of the pointer is this rect's pivot, and any lean or
	// size is authored on the object itself.
	[RequireComponent(typeof(RectTransform))]
	public class CursorVisual : MonoBehaviour
	{
		[SerializeField] private CursorVisualState _state = CursorVisualState.Skull;

		public CursorVisualState State => _state;

		public RectTransform Rect => (RectTransform)transform;
	}
}
