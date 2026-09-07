using Sirenix.OdinInspector;
using Unity.Cinemachine;
using UnityEngine;

namespace Game.Runtime.Player.Camera
{
	// A shot the brain blends to, aimed at whatever the request handed over. The framing, the lens and
	// the damping all stay on the CinemachineCamera where a designer can see them — this only decides
	// when it is live and what it is pointed at, and never touches Camera.transform.
	//
	// One class for every such state: looking at a card and focusing on a player differ in the shot,
	// not in the mechanism, so they are two authored objects rather than two scripts.
	public class PlayerCameraVirtualCameraState : PlayerCameraState
	{
		[Header("Shot")]
		[Required]
		[SerializeField] private CinemachineCamera _camera;

		[Tooltip("What it is raised to while live. It only has to beat the rig's own camera, which sits at the default.")]
		[SerializeField] private int _priority = 20;

		[Header("Look")]
		[Tooltip("On, the player cannot turn their own view while this shot is up. A view being aimed on somebody's behalf that they can also turn is two hands on one wheel.")]
		[SerializeField] private bool _suspendsLook = true;

		protected override void OnInitialize()
		{
			// Off until it is asked for. Every *enabled* virtual camera is a candidate and the rig's own
			// sits at the default priority, so a shot left live at zero is a tie the brain may break
			// either way — which reads as the view occasionally starting out pointed at nothing.
			SetLive(false);
		}

		protected override void OnEnter()
		{
			Aim();
			SetLive(true);

			if (_suspendsLook && Controller && Controller.PlayerController) Controller.PlayerController.SetLookSuspended(true);
		}

		protected override void OnRetarget() => Aim();

		protected override void OnExit()
		{
			SetLive(false);

			// The look is not handed back here: whichever state comes next says what it wants, and the
			// one that always follows is free flight, which hands it back itself. Releasing it from both
			// ends would be two answers to one question.
		}

		private void Aim()
		{
			if (!_camera || !Target) return;

			_camera.Target.LookAtTarget = Target;

			// Only if the shot was authored to follow something. A shot meant to stay at the player's own
			// eye has no tracking target on purpose, and filling one in would walk it off the body.
			if (_camera.Target.TrackingTarget) _camera.Target.TrackingTarget = Target;
		}

		// The component rather than the GameObject: this state lives on the same object, and switching
		// that off would stop the very thing that has to switch it back on.
		private void SetLive(bool live)
		{
			if (!_camera) return;

			_camera.Priority.Enabled = true;
			_camera.Priority.Value = _priority;
			_camera.enabled = live;
		}
	}
}
