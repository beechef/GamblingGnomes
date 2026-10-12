using System.Collections.Generic;
using FMODUnity;
using UnityEngine;

namespace Game.Runtime.Audio
{
	// Each sound is its own instance. A one-shot is released as soon as it starts, so FMOD frees it when it
	// ends; one that plays until stopped is kept by id and released on Stop. An attached one is followed by
	// RuntimeManager until it stops.
	public class FmodAudioBackend : IAudioBackend
	{
		private readonly Dictionary<int, FMOD.Studio.EventInstance> _playing = new();
		private int _nextId;

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

		public int Play(AudioEvent audioEvent, Transform target)
		{
			if (!TryCreate(audioEvent, out var instance)) return 0;

			if (target)
			{
				instance.set3DAttributes(target.To3DAttributes());
				RuntimeManager.AttachInstanceToGameObject(instance, target.gameObject);
			}

			instance.setVolume(audioEvent.Volume);
			instance.start();

			var id = ++_nextId;
			_playing[id] = instance;
			return id;
		}

		public void Stop(int id, bool fadeOut)
		{
			if (!_playing.Remove(id, out var instance) || !instance.isValid()) return;

			instance.stop(fadeOut ? FMOD.Studio.STOP_MODE.ALLOWFADEOUT : FMOD.Studio.STOP_MODE.IMMEDIATE);
			instance.release();
		}

		public void SetParameter(int id, string parameter, float value)
		{
			if (_playing.TryGetValue(id, out var instance) && instance.isValid()) instance.setParameterByName(parameter, value);
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
