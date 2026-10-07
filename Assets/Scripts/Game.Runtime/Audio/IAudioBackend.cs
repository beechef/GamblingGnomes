using UnityEngine;

namespace Game.Runtime.Audio
{
	public interface IAudioBackend
	{
		void PlayOneShot(AudioEvent audioEvent, Vector3 position);

		void PlayOneShotAttached(AudioEvent audioEvent, Transform target);
	}
}
