using DG.Tweening;
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

		[Header("Size")]
		[Tooltip("On, the word grows while pointed at, held or chosen. Off leaves the authored size alone.")]
		[SerializeField] private bool _animateSize;

		[ShowIf(nameof(_animateSize)), MinValue(1f)]
		[SerializeField] private float _normalSize = 40f;

		[ShowIf(nameof(_animateSize)), MinValue(1f)]
		[SerializeField] private float _hoveredSize = 64f;

		[ShowIf(nameof(_animateSize)), MinValue(0f)]
		[SerializeField] private float _sizeDuration = 0.12f;

		[ShowIf(nameof(_animateSize))]
		[SerializeField] private Ease _sizeEase = Ease.OutBack;

		private Tween _sizeTween;

		protected override void OnReset()
		{
			_label = GetComponentInChildren<TextMeshProUGUI>();
		}

		protected override void OnDisabled()
		{
			_sizeTween?.Kill();
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

			if (_animateSize) ApplySize(state is UIButtonState.Hovered or UIButtonState.Selected or UIButtonState.Pressed ? _hoveredSize : _normalSize, instant);
		}

		// An auto-sized label grows through its ceiling, so a long word still shrinks to fit instead of overflowing.
		private void ApplySize(float size, bool instant)
		{
			_sizeTween?.Kill();

			if (instant || _sizeDuration <= 0f)
			{
				SetSize(size);
				return;
			}

			_sizeTween = DOTween.To(GetSize, SetSize, size, _sizeDuration)
				.SetEase(_sizeEase)
				.SetUpdate(true)
				.SetLink(gameObject);
		}

		private float GetSize() => _label.enableAutoSizing ? _label.fontSizeMax : _label.fontSize;

		private void SetSize(float size)
		{
			if (_label.enableAutoSizing) _label.fontSizeMax = size;
			else _label.fontSize = size;
		}
	}
}
