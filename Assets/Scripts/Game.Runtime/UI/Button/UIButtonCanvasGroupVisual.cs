using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.UI.Button
{
	// Fades a whole button — art, label, badge — through one CanvasGroup, so a state reads as the button
	// coming forward or stepping back rather than as each piece being tinted apart. Alpha only: whether it
	// can be pressed is UIButton's answer, and a visual writing interactable or blocksRaycasts would be
	// the drawing half deciding a rule. Author the group's own alpha at the normal value, so edit mode
	// shows the button as it rests.
	public class UIButtonCanvasGroupVisual : UIButtonVisual
	{
		[Header("Target")]
		[Required]
		[SerializeField] private CanvasGroup _target;

		[Header("Alpha")]
		[PropertyRange(0f, 1f)]
		[SerializeField] private float _normalAlpha = 1f;

		[PropertyRange(0f, 1f)]
		[SerializeField] private float _hoveredAlpha = 1f;

		[PropertyRange(0f, 1f)]
		[SerializeField] private float _pressedAlpha = 0.85f;

		[PropertyRange(0f, 1f)]
		[SerializeField] private float _disabledAlpha = 0.5f;

		[Header("Timing")]
		[MinValue(0f)]
		[SerializeField] private float _duration = 0.12f;

		[SerializeField] private Ease _ease = Ease.OutQuad;

		private Tween _tween;

		private void OnDestroy()
		{
			KillTween();
		}

		protected override void OnApply(UIButtonState state, bool instant)
		{
			if (!_target) return;

			var alpha = AlphaFor(state);

			KillTween();

			if (instant || _duration <= 0f)
			{
				_target.alpha = alpha;
				return;
			}

			// By value, because DOTween's UI module is not in this project. Unscaled so a button still
			// answers while the pause menu has the game stopped, and linked so the tween dies with the
			// object rather than writing into a destroyed group.
			_tween = DOTween.To(() => _target.alpha, value => _target.alpha = value, alpha, _duration)
				.SetEase(_ease)
				.SetUpdate(true)
				.SetLink(gameObject);
		}

		private float AlphaFor(UIButtonState state) => state switch
		{
			UIButtonState.Hovered or UIButtonState.Selected => _hoveredAlpha,
			UIButtonState.Pressed => _pressedAlpha,
			UIButtonState.Disabled => _disabledAlpha,
			_ => _normalAlpha
		};

		private void KillTween()
		{
			_tween?.Kill();
			_tween = null;
		}
	}
}
