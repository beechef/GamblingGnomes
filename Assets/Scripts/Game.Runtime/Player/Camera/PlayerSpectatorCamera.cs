using System;
using Unity.Cinemachine;
using UnityEngine;

namespace Game.Runtime.Player.Camera
{
	// A shot of this player from outside, for another client to cut to while something is happening to
	// them. It lives on the body it frames, so it follows them into whatever chair they sit in and nobody
	// else has to know where that is.
	//
	// Off until somebody asks, and priority dropped with it: every player in the room carries one, and a
	// live candidate left standing on each body would outrank the view its own client is looking through.
	// Requests are counted, the same shape as the camera stack and the cursor's release, so two things on
	// one machine wanting it cannot switch each other off.
	public class PlayerSpectatorCamera : MonoBehaviour
	{
		[SerializeField] private CinemachineCamera _camera;

		[Tooltip("Priority while somebody is watching through it. Has to outrank the first-person camera, which sits at the default.")]
		[SerializeField] private int _livePriority = 20;

		// Whether this client is looking through any spectator camera at all. A view from outside is where the
		// local player's own body can be in shot, so whatever decides which rig is drawn listens to this.
		public static bool AnyLive => _liveCount > 0;

		public static event Action<bool> OnAnyLiveChanged;

		private static int _liveCount;

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void ResetStatics()
		{
			_liveCount = 0;
			OnAnyLiveChanged = null;
		}

		private int _holds;
		private bool _live;

		private void Awake()
		{
			if (!_camera) _camera = GetComponent<CinemachineCamera>();

			SetLive(false);
		}

		public void Hold()
		{
			_holds++;

			if (_holds == 1) SetLive(true);
		}

		public void Release()
		{
			if (_holds == 0) return;

			_holds--;

			if (_holds == 0) SetLive(false);
		}

		private void OnDisable()
		{
			_holds = 0;
			SetLive(false);
		}

		private void SetLive(bool live)
		{
			if (_camera)
			{
				_camera.Priority = live ? _livePriority : 0;
				_camera.enabled = live;
			}

			if (_live == live) return;

			_live = live;
			_liveCount += live ? 1 : -1;

			if (_liveCount == (live ? 1 : 0)) OnAnyLiveChanged?.Invoke(live);
		}
	}
}
