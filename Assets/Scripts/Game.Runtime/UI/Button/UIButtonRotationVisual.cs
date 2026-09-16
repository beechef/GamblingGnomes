using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.UI.Button
{
	// A fourth backend beside sprite, scale and offset: the button tilts under the pointer and kicks the
	// other way under the press. Angles are measured from whatever rotation the target was authored with,
	// so the same component works on art that already sits at a slant.
	public class UIButtonRotationVisual : UIButtonVisual
	{
		[Header("Target")]
		[Required]
		[SerializeField] private RectTransform _target;

		[Header("Rotation")]
		[Tooltip("Tilt under the pointer, in degrees about Z. Positive is counter-clockwise.")]
		[SerializeField] private float _hoveredAngle = 4f;

		[Tooltip("Kick under the press. Released while still hovered, the button returns to the tilt rather than to rest — that is the state machine's answer, not a special case here.")]
		[SerializeField] private float _pressedAngle = -4f;

		[SerializeField] private float _disabledAngle;

		[Header("Timing")]
		[MinValue(0f)]
		[SerializeField] private float _duration = 0.1f;

		[SerializeField] private Ease _ease = Ease.OutBack;

		private Tween _tween;
		private Quaternion _restRotation;
		private float _angle;
		private bool _restCaptured;

		protected override void OnReset()
		{
			_target = transform as RectTransform;
		}

		private void Awake()
		{
			CaptureRest();
		}

		private void OnDestroy()
		{
			KillTween();
		}

		// Read once, before any tilt has been applied. Taking the live rotation on every state change
		// would fold the previous tilt into the rest and spin the button off its authored angle one
		// press at a time — the same defect UIButtonOffsetVisual captures its rest position for.
		private void CaptureRest()
		{
			if (_restCaptured || !_target) return;

			_restRotation = _target.localRotation;
			_restCaptured = true;
		}

		protected override void OnApply(UIButtonState state, bool instant)
		{
			if (!_target) return;

			CaptureRest();

			var target = AngleFor(state);

			KillTween();

			if (instant || _duration <= 0f)
			{
				ApplyAngle(target);
				return;
			}

			// Tweened as an angle written through the rest rotation rather than as a euler on the
			// transform: composing keeps whatever slant the art was authored with, and a single float
			// cannot take the long way round the way an interpolated euler triple can. Unscaled so a
			// button still answers while the pause menu has the game stopped, and linked so the tween
			// dies with the object rather than writing into a destroyed transform.
			_tween = DOTween.To(() => _angle, ApplyAngle, target, _duration)
				.SetEase(_ease)
				.SetUpdate(true)
				.SetLink(gameObject);
		}

		private void ApplyAngle(float angle)
		{
			_angle = angle;
			_target.localRotation = _restRotation * Quaternion.Euler(0f, 0f, angle);
		}

		private float AngleFor(UIButtonState state) => state switch
		{
			UIButtonState.Hovered or UIButtonState.Selected => _hoveredAngle,
			UIButtonState.Pressed => _pressedAngle,
			UIButtonState.Disabled => _disabledAngle,
			_ => 0f
		};

		private void KillTween()
		{
			_tween?.Kill();
			_tween = null;
		}
	}
}
