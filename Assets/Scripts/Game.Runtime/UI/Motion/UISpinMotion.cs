using DG.Tweening;
using UnityEngine;

namespace Game.Runtime.UI.Motion
{
	// Turns at an even speed for as long as it is on, in unscaled time so a paused game still spins. Owns this
	// object's rotation.
	[RequireComponent(typeof(RectTransform))]
	public class UISpinMotion : MonoBehaviour
	{
		[Tooltip("Seconds for one full turn.")]
		[Min(0.05f)]
		[SerializeField] private float _turnDuration = 1f;

		[Tooltip("On, it turns clockwise.")]
		[SerializeField] private bool _clockwise = true;

		private Quaternion _restRotation;
		private Tween _spin;

		private void Awake() => _restRotation = transform.localRotation;

		private void OnEnable()
		{
			_spin = transform.DOLocalRotate(new Vector3(0f, 0f, _clockwise ? -360f : 360f), _turnDuration, RotateMode.LocalAxisAdd)
				.SetEase(Ease.Linear).SetLoops(-1, LoopType.Incremental).SetUpdate(true);
		}

		private void OnDisable()
		{
			_spin?.Kill();
			transform.localRotation = _restRotation;
		}
	}
}
