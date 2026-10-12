using UnityEngine;

namespace Game.Runtime.Audio
{
	public interface IAudioBackend
	{
		void PlayOneShot(AudioEvent audioEvent, Vector3 position);

		void PlayOneShotAttached(AudioEvent audioEvent, Transform target);

		// Plays until Stop; 0 when nothing could be played. A null target plays it unplaced.
		int Play(AudioEvent audioEvent, Transform target);

		void Stop(int id, bool fadeOut);

		void SetParameter(int id, string parameter, float value);
	}
}
