using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Runtime.UI.Button
{
	// The art half: one sprite per state, swapped as the state changes. A state left empty falls back to
	// the normal sprite, so a button only needs the cuts its art actually has — a set with no hover
	// frame simply does not change on hover instead of blanking.
	public class UIButtonSpriteVisual : UIButtonVisual
	{
		[Header("Target")]
		[Required]
		[SerializeField] private Image _image;

		[Header("Sprites")]
		[Required]
		[SerializeField] private Sprite _normal;

		[SerializeField] private Sprite _hovered;
		[SerializeField] private Sprite _pressed;
		[SerializeField] private Sprite _disabled;

		[Header("Tint")]
		[Tooltip("Multiplied over the sprite. Leave every colour white for art that carries its own states.")]
		[SerializeField] private Color _normalColor = Color.white;

		[SerializeField] private Color _hoveredColor = Color.white;
		[SerializeField] private Color _pressedColor = Color.white;
		[SerializeField] private Color _disabledColor = Color.white;

		private void Reset()
		{
			_image = GetComponent<Image>();
			if (_image) _normal = _image.sprite;
		}

		protected override void OnApply(UIButtonState state, bool instant)
		{
			if (!_image) return;

			_image.sprite = SpriteFor(state);
			_image.color = ColorFor(state);
		}

		private Sprite SpriteFor(UIButtonState state)
		{
			// Selected borrows the hovered art rather than carrying a fourth set: a plate button is
			// marked by the pointer beside it, and inventing a state for every button that never gets
			// selected would only add sprites nobody fills in.
			var sprite = state switch
			{
				UIButtonState.Hovered or UIButtonState.Selected => _hovered,
				UIButtonState.Pressed => _pressed,
				UIButtonState.Disabled => _disabled,
				_ => _normal
			};

			return sprite ? sprite : _normal;
		}

		private Color ColorFor(UIButtonState state) => state switch
		{
			UIButtonState.Hovered or UIButtonState.Selected => _hoveredColor,
			UIButtonState.Pressed => _pressedColor,
			UIButtonState.Disabled => _disabledColor,
			_ => _normalColor
		};
	}
}
