using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.UI.Button
{
	// Turns the target round and round while the button is pointed at, and brings it back to face the
	// viewer when it is left. Works on any Transform — a sprite, a 3D model on the UI, a whole group — since
	// a button is its state, not its art. The turn is measured from the rotation captured once in Awake, so
	// a model authored at an angle spins about that angle rather than snapping square first.
	public class UIButtonSpinVisual : UIButtonVisual
	{
		[Header("Target")]
		[Required]
		[SerializeField] private Transform _target;

		[Header("Spin")]
		[Tooltip("Local axis the target turns about.")]
		[SerializeField] private Vector3 _axis = Vector3.up;

		[Tooltip("Degrees per second while hovered, selected or pressed. Negative turns the other way.")]
		[SerializeField] private float _degreesPerSecond = 180f;

		[Header("Return")]
		[Tooltip("How long it takes to come back to rest once the pointer leaves, by the shortest way round.")]
		[MinValue(0f)]
		[SerializeField] private float _returnDuration = 0.25f;

		[SerializeField] private Ease _returnEase = Ease.OutCubic;

		private Tween _tween;
		private Quaternion _restRotation;
		private float _angle;
		private bool _restCaptured;
		private bool _spinning;

		protected override void OnReset()
		{
			_target = transform;
		}

		private void Awake()
		{
			CaptureRest();
		}

		protected override void OnDisabled()
		{
			KillTween();
			_spinning = false;
			if (_restCaptured && _target) ApplyAngle(0f);
		}

		private void OnDestroy()
		{
			KillTween();
		}

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

			var spin = state is UIButtonState.Hovered or UIButtonState.Selected or UIButtonState.Pressed;

			// Hovered to pressed and back is still one spin; restarting it would hitch the turn.
			if (spin && _spinning) return;

			_spinning = spin;
			KillTween();

			if (spin)
			{
				StartSpin();
				return;
			}

			var from = Mathf.DeltaAngle(0f, _angle);

			if (instant || _returnDuration <= 0f)
			{
				ApplyAngle(0f);
				return;
			}

			ApplyAngle(from);

			// Unscaled and linked, like every button visual.
			_tween = DOTween.To(() => _angle, ApplyAngle, 0f, _returnDuration)
				.SetEase(_returnEase)
				.SetUpdate(true)
				.SetLink(gameObject);
		}

		// One full turn repeated incrementally, so the speed is the only number and the angle never wraps.
		private void StartSpin()
		{
			if (Mathf.Approximately(_degreesPerSecond, 0f)) return;

			var turn = Mathf.Sign(_degreesPerSecond) * 360f;

			_tween = DOTween.To(() => _angle, ApplyAngle, _angle + turn, 360f / Mathf.Abs(_degreesPerSecond))
				.SetEase(Ease.Linear)
				.SetLoops(-1, LoopType.Incremental)
				.SetUpdate(true)
				.SetLink(gameObject);
		}

		private void ApplyAngle(float angle)
		{
			_angle = angle;
			_target.localRotation = _restRotation * Quaternion.AngleAxis(angle, _axis);
		}

		private void KillTween()
		{
			_tween?.Kill();
			_tween = null;
		}
	}
}
