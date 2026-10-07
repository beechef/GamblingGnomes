using FMODUnity;
using UnityEngine;

namespace Game.Runtime.Audio
{
	// Each sound is its own instance, released as soon as it starts: FMOD frees it when it stops, and an
	// attached one is followed by RuntimeManager until then.
	public class FmodAudioBackend : IAudioBackend
	{
		public FmodAudioBackend(GameObject listener)
		{
			if (listener && !listener.TryGetComponent(out StudioListener _)) listener.AddComponent<StudioListener>();
		}

		public void PlayOneShot(AudioEvent audioEvent, Vector3 position)
		{
			if (!TryCreate(audioEvent, out var instance)) return;

			instance.set3DAttributes(position.To3DAttributes());
			Start(instance, audioEvent);
		}

		public void PlayOneShotAttached(AudioEvent audioEvent, Transform target)
		{
			if (!TryCreate(audioEvent, out var instance)) return;

			instance.set3DAttributes(target.To3DAttributes());
			RuntimeManager.AttachInstanceToGameObject(instance, target.gameObject);
			Start(instance, audioEvent);
		}

		private static bool TryCreate(AudioEvent audioEvent, out FMOD.Studio.EventInstance instance)
		{
			instance = default;

			if (audioEvent.FmodEvent.IsNull)
			{
				Debug.LogWarning($"{nameof(FmodAudioBackend)}: {audioEvent.name} has no FMOD event.", audioEvent);
				return false;
			}

			instance = RuntimeManager.CreateInstance(audioEvent.FmodEvent);
			return instance.isValid();
		}

		private static void Start(FMOD.Studio.EventInstance instance, AudioEvent audioEvent)
		{
			instance.setVolume(audioEvent.Volume);
			instance.start();
			instance.release();
		}
	}
}
