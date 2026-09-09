using Sirenix.OdinInspector;
using Unity.Cinemachine;
using UnityEngine;

namespace Game.Runtime.Player.Camera
{
	// A shot the brain blends to, aimed at whatever the request handed over. The framing, the lens and
	// the damping all stay on the CinemachineCamera where a designer can see them — this only decides
	// when it is live and what it is pointed at, and never touches Camera.transform.
	//
	// One class for every such state: looking at a card and focusing on a player differ in the shot, not
	// in the mechanism, so they are two authored objects rather than two scripts.
	public class PlayerCameraVirtualCameraState : PlayerCameraState
	{
		[Header("Shot")]
		[Required]
		[SerializeField] private CinemachineCamera _camera;

		[Tooltip("What it is raised to while live. It only has to beat the rig's own camera, which sits at the default.")]
		[SerializeField] private int _priority = 20;

		[Header("Position")]
		[Tooltip("On, the shot sits exactly where the player's own eye is and only turns. Off, it stays wherever its transform was authored — which is a guess about a seated pose, and the reason the first version framed the floor. Needs a Hard Lock To Target body on the camera.")]
		[SerializeField] private bool _lockToEye = true;

		[Header("Look")]
		[Tooltip("On, the player cannot turn their view at all while this shot is up — not merely handed over, but off. A view being aimed on somebody's behalf that they can also turn is two hands on one wheel.")]
		[SerializeField] private bool _disablesLook = true;

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

			if (_disablesLook) SetLook(false);
		}

		protected override void OnRetarget() => Aim();

		protected override void OnExit()
		{
			SetLive(false);

			// The look is not handed back here: whichever state comes next says what it wants, and the one
			// that always follows is free flight, which hands it back itself. Releasing it from both ends
			// would be two answers to one question.
		}

		private void Aim()
		{
			if (!_camera) return;

			// Where it sits: the eye this client is actually rendering through, so the shot starts from
			// the player's own view and only swings onto the target. A transform authored at a fixed local
			// height is a guess about a pose — the gnome is seated, and the prefab is not.
			if (_lockToEye && Controller)
			{
				var eye = Controller.Eye;
				if (eye) _camera.Target.TrackingTarget = eye;
			}

			if (Target) _camera.Target.LookAtTarget = Target;
		}

		// The component rather than the GameObject: this state lives on the same object, and switching
		// that off would stop the very thing that has to switch it back on.
		//
		// The priority is raised only on the way up and dropped on the way down. Left standing at 20 on a
		// shot nobody has asked for, every player in the room is carrying a camera that outranks the one
		// this client is looking through the moment anything switches it on — and the copies of this that
		// run on other people's bodies are exactly the ones nobody is watching.
		private void SetLive(bool live)
		{
			if (!_camera) return;

			_camera.Priority.Enabled = live;
			_camera.Priority.Value = live ? _priority : 0;
			_camera.enabled = live;
		}

		// The input and nothing else. Suspending the look as well is what made the first frame of this shot
		// flick: suspension stops the look being *applied* to the bone, the Animator puts that bone back to
		// the clip's pose on the very next frame, and the rendered camera hangs off it — so the shot the
		// brain is blending *away from* jumped in the same instant the blend began. Disabling the input
		// freezes the angles where the player left them, which is what a body being looked at from
		// somewhere else should hold anyway; the bone is not something a camera state ever needs.
		private void SetLook(bool enabled)
		{
			if (!Controller || !Controller.PlayerController) return;

			Controller.PlayerController.SetLookInputDisabled(!enabled);
		}
	}
}
