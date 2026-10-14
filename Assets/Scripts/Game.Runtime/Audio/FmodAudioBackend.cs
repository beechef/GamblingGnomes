using System.Collections.Generic;
using FMODUnity;
using UnityEngine;

namespace Game.Runtime.Audio
{
	// Each sound is its own instance. A one-shot is released as soon as it starts, so FMOD frees it when it
	// ends; one that plays until stopped is kept by id and released on Stop. An attached one is followed by
	// RuntimeManager until it stops. Lookups go through the Studio system's RESULT calls, never RuntimeManager's
	// throwing ones: without banks (a Multiplayer Play Mode clone runs outside the project root, where they are
	// built) every sound is skipped, rather than the exception killing whatever played it (a button's click).
	public class FmodAudioBackend : IAudioBackend
	{
		private readonly Dictionary<int, FMOD.Studio.EventInstance> _playing = new();
		private int _nextId;
		private bool _warnedMissing;

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

		// Paths authored in the FMOD project: the master bus and one VCA per player-facing level.
		public void SetVolumes(float master, float music, float sfx)
		{
			var studio = RuntimeManager.StudioSystem;

			if (studio.getBus("bus:/", out var masterBus) == FMOD.RESULT.OK) masterBus.setVolume(master);
			else WarnMissing("bus:/");

			if (studio.getVCA("vca:/MUSIC", out var musicVca) == FMOD.RESULT.OK) musicVca.setVolume(music);
			else WarnMissing("vca:/MUSIC");

			if (studio.getVCA("vca:/SFX", out var sfxVca) == FMOD.RESULT.OK) sfxVca.setVolume(sfx);
			else WarnMissing("vca:/SFX");
		}

		private bool TryCreate(AudioEvent audioEvent, out FMOD.Studio.EventInstance instance)
		{
			instance = default;

			if (audioEvent.FmodEvent.IsNull)
			{
				Debug.LogWarning($"{nameof(FmodAudioBackend)}: {audioEvent.name} has no FMOD event.", audioEvent);
				return false;
			}

			if (RuntimeManager.StudioSystem.getEventByID(audioEvent.FmodEvent.Guid, out var description) != FMOD.RESULT.OK)
			{
				WarnMissing(audioEvent.name);
				return false;
			}

			return description.createInstance(out instance) == FMOD.RESULT.OK && instance.isValid();
		}

		private void WarnMissing(string what)
		{
			if (_warnedMissing) return;

			_warnedMissing = true;
			Debug.LogWarning($"{nameof(FmodAudioBackend)}: '{what}' is not in the loaded banks; sounds are skipped (no banks in a Multiplayer Play Mode clone?).");
		}

		private static void Start(FMOD.Studio.EventInstance instance, AudioEvent audioEvent)
		{
			instance.setVolume(audioEvent.Volume);
			instance.start();
			instance.release();
		}
	}
}
