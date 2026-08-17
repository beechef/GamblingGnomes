using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.UI.Button
{
	// A mark laid over the button for one state and taken away again — the cross on a move the rules do
	// not allow. A fourth backend rather than a branch inside the sprite one, so a button can be dimmed,
	// crossed, both, or neither, by which visuals it carries.
	public class UIButtonOverlayVisual : UIButtonVisual
	{
		[Header("Target")]
		[Required]
		[Tooltip("Shown only while the button stands in the state below. Kept as a child so the mark can be art of any size, over art of any shape.")]
		[SerializeField] private GameObject _overlay;

		[Header("State")]
		[SerializeField] private UIButtonState _shownState = UIButtonState.Disabled;

		protected override void OnApply(UIButtonState state, bool instant)
		{
			if (!_overlay) return;

			var shown = state == _shownState;
			if (_overlay.activeSelf != shown) _overlay.SetActive(shown);
		}
	}
}
