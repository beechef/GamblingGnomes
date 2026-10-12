using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.Audio
{
	// The one door every sound goes through. Callers name an AudioEvent; the backend decides how it plays.
	public class AudioManager : MonoBehaviour
	{
		public static AudioManager Instance { get; private set; }

		[Tooltip("The camera the game is heard from. The backend puts its listener on it.")]
		[Required]
		[SerializeField] private GameObject _listener;

		private IAudioBackend _backend;

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void ResetStatics() => Instance = null;

		private void Awake()
		{
			if (Instance && Instance != this)
			{
				Destroy(gameObject);
				return;
			}

			Instance = this;
			_backend = new FmodAudioBackend(_listener);
		}

		private void OnDestroy()
		{
			if (Instance == this) Instance = null;
		}

		public void PlayOneShot(AudioEvent audioEvent, Vector3 position)
		{
			if (audioEvent && _backend != null) _backend.PlayOneShot(audioEvent, position);
		}

		public void PlayOneShotAttached(AudioEvent audioEvent, Transform target)
		{
			if (!audioEvent || _backend == null) return;

			if (target) _backend.PlayOneShotAttached(audioEvent, target);
			else _backend.PlayOneShot(audioEvent, transform.position);
		}

		// For a sound that lasts until something ends it (music, ambience); the caller keeps the handle and
		// stops it. A null target plays it unplaced, the way music is heard.
		public AudioHandle Play(AudioEvent audioEvent, Transform target = null)
		{
			if (!audioEvent || _backend == null) return default;

			return new AudioHandle(_backend.Play(audioEvent, target));
		}

		public void Stop(AudioHandle handle, bool fadeOut = true)
		{
			if (handle.IsValid && _backend != null) _backend.Stop(handle.Id, fadeOut);
		}

		// A parameter on a sound that lasts, by the name its event gives it.
		public void SetParameter(AudioHandle handle, string parameter, float value)
		{
			if (handle.IsValid && _backend != null && !string.IsNullOrEmpty(parameter)) _backend.SetParameter(handle.Id, parameter, value);
		}
	}
}
