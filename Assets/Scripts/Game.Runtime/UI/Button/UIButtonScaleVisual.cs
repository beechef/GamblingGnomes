using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.UI.Button
{
	// The motion half, kept apart from the art so a button can swap sprites, or lean, or both. Every
	// number is serialized because the feel of a button is tuned by eye, not argued about in code.
	public class UIButtonScaleVisual : UIButtonVisual
	{
		[Header("Target")]
		[Required]
		[SerializeField] private RectTransform _target;

		[Header("Scale")]
		[MinValue(0.1f)]
		[SerializeField] private float _normalScale = 1f;

		[MinValue(0.1f)]
		[SerializeField] private float _hoveredScale = 1.06f;

		[MinValue(0.1f)]
		[SerializeField] private float _pressedScale = 0.94f;

		[MinValue(0.1f)]
		[SerializeField] private float _disabledScale = 1f;

		[Header("Timing")]
		[MinValue(0f)]
		[SerializeField] private float _duration = 0.12f;

		[SerializeField] private Ease _ease = Ease.OutBack;

		private Tween _tween;

		protected override void OnReset()
		{
			_target = transform as RectTransform;
		}

		private void OnDestroy()
		{
			KillTween();
		}

		protected override void OnApply(UIButtonState state, bool instant)
		{
			if (!_target) return;

			var scale = Vector3.one * ScaleFor(state);

			KillTween();

			if (instant || _duration <= 0f)
			{
				_target.localScale = scale;
				return;
			}

			// Unscaled so a button still answers while the pause menu has the game stopped, and linked so
			// the tween dies with the object rather than writing into a destroyed transform.
			_tween = _target.DOScale(scale, _duration)
				.SetEase(_ease)
				.SetUpdate(true)
				.SetLink(gameObject);
		}

		// A pop for attention (something arrived) from the scale this state rests at; the next state change takes over.
		public void Punch(Vector3 punch, float duration, int vibrato, float elasticity)
		{
			if (!_target || !Button) return;

			KillTween();
			_target.localScale = Vector3.one * ScaleFor(Button.State);
			_tween = _target.DOPunchScale(punch, duration, vibrato, elasticity)
				.SetUpdate(true)
				.SetLink(gameObject);
		}

		private float ScaleFor(UIButtonState state) => state switch
		{
			UIButtonState.Hovered or UIButtonState.Selected => _hoveredScale,
			UIButtonState.Pressed => _pressedScale,
			UIButtonState.Disabled => _disabledScale,
			_ => _normalScale
		};

		private void KillTween()
		{
			_tween?.Kill();
			_tween = null;
		}
	}
}
