using UnityEngine;

namespace Game.Runtime.Controller
{
	// The camera everything renders through, declared rather than discovered. Camera.main only answers
	// for a camera tagged MainCamera, and a scene scan only answers for whatever happens to be loaded at
	// the moment somebody asks — this component is the one place that says outright which camera it is.
	[RequireComponent(typeof(Camera))]
	public class GameCamera : MonoBehaviour
	{
		public static Camera Main { get; private set; }
		public static Transform View => Main ? Main.transform : null;

		// Domain Reload is disabled, so statics survive between play sessions.
		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void ResetStatics() => Main = null;

		[Header("References")]
		[SerializeField] private Camera _camera;

		private void Awake()
		{
			if (!_camera) _camera = GetComponent<Camera>();

			if (Main && Main != _camera)
			{
				Debug.LogWarning("[GameCamera] A second game camera claimed the view — the newest one wins.");
			}

			Main = _camera;
		}

		private void OnDestroy()
		{
			if (Main == _camera) Main = null;
		}
	}
}
