using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

namespace Game.Runtime.UI.Button
{
	// For buttons that are nothing but their lettering — a menu list, a text link. The plate never
	// changes, the word does: the one being pointed at takes the highlight colour and the rest stay
	// quiet, which is the whole visual language of a menu.
	public class UIButtonLabelVisual : UIButtonVisual
	{
		[Header("Target")]
		[Required]
		[SerializeField] private TextMeshProUGUI _label;

		[Header("Colours")]
		[SerializeField] private Color _normalColor = new(0.85f, 0.78f, 0.65f);
		[SerializeField] private Color _hoveredColor = new(0.95f, 0.90f, 0.78f);
		[SerializeField] private Color _selectedColor = new(0.78f, 0.18f, 0.15f);
		[SerializeField] private Color _pressedColor = new(0.60f, 0.12f, 0.10f);
		[SerializeField] private Color _disabledColor = new(0.45f, 0.42f, 0.38f);

		private void Reset()
		{
			_label = GetComponentInChildren<TextMeshProUGUI>();
		}

		protected override void OnApply(UIButtonState state, bool instant)
		{
			if (!_label) return;

			_label.color = state switch
			{
				UIButtonState.Hovered => _hoveredColor,
				UIButtonState.Selected => _selectedColor,
				UIButtonState.Pressed => _pressedColor,
				UIButtonState.Disabled => _disabledColor,
				_ => _normalColor
			};
		}
	}
}
