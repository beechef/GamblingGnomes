using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.UI.Button
{
	// A third backend beside sprite and scale: the button lifts under the pointer and pushes down under
	// the press. Offsets are measured from wherever the target was authored, so the same component works
	// on four petals sitting at four different places — nothing here needs to know its own address.
	public class UIButtonOffsetVisual : UIButtonVisual
	{
		[Header("Target")]
		[Required]
		[SerializeField] private RectTransform _target;

		[Header("Offset")]
		[Tooltip("Lift under the pointer. Positive is up.")]
		[SerializeField] private Vector2 _hoveredOffset = new(0f, 6f);

		[Tooltip("Push under the press. Released while still hovered, the button returns to the lift rather than to rest — that is the state machine's answer, not a special case here.")]
		[SerializeField] private Vector2 _pressedOffset = new(0f, -6f);

		[SerializeField] private Vector2 _disabledOffset = Vector2.zero;

		[Header("Timing")]
		[MinValue(0f)]
		[SerializeField] private float _duration = 0.1f;

		[SerializeField] private Ease _ease = Ease.OutQuad;

		private Tween _tween;
		private Vector2 _restPosition;
		private bool _restCaptured;

		private void Reset()
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

		// Read once, before any offset has been applied. Taking it later — or taking the live position on
		// every state change — would fold the previous offset into the rest and walk the button off its
		// authored spot one press at a time.
		private void CaptureRest()
		{
			if (_restCaptured || !_target) return;

			_restPosition = _target.anchoredPosition;
			_restCaptured = true;
		}

		protected override void OnApply(UIButtonState state, bool instant)
		{
			if (!_target) return;

			CaptureRest();

			var target = _restPosition + OffsetFor(state);

			KillTween();

			if (instant || _duration <= 0f)
			{
				_target.anchoredPosition = target;
				return;
			}

			// DOTween.To rather than DOAnchorPos: that shortcut lives in DOTween's UI module, which this
			// project does not carry. Unscaled so a button still answers while the game is stopped, and
			// linked so the tween dies with the object rather than writing into a destroyed transform.

			if (state is UIButtonState.Pressed)
			{
				//If pressed, we want to play forward and then back to the target position, so we use a sequence.
				
				_tween = DOTween.Sequence()
					.Append(DOTween.To(() => _target.anchoredPosition, position => _target.anchoredPosition = position,
						target, _duration)
						.SetEase(_ease))
					.Append(DOTween.To(() => _target.anchoredPosition, position => _target.anchoredPosition = position,
						_restPosition, _duration)
						.SetEase(_ease))
					.SetUpdate(true)
					.SetLink(gameObject);
			}
			else
			{
				_tween = DOTween.To(() => _target.anchoredPosition, position => _target.anchoredPosition = position,
						target, _duration)
					.SetEase(_ease)
					.SetUpdate(true)
					.SetLink(gameObject);
			}
		}

		private Vector2 OffsetFor(UIButtonState state) => state switch
		{
			UIButtonState.Hovered or UIButtonState.Selected => _hoveredOffset,
			UIButtonState.Pressed => _pressedOffset,
			UIButtonState.Disabled => _disabledOffset,
			_ => Vector2.zero
		};

		private void KillTween()
		{
			_tween?.Kill();
			_tween = null;
		}
	}
}
