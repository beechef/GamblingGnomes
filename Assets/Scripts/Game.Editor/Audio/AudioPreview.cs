using System.Collections.Generic;
using FMODUnity;
using Game.Runtime.Audio;
using UnityEditor;
using UnityEngine;

namespace Game.Editor.Audio
{
	// Plays an AudioEvent outside Play mode through FMOD's own editor preview system and banks, heard flat
	// since the editor has no listener to place it against. In Play mode it goes through AudioManager.
	public static class AudioPreview
	{
		private static readonly Dictionary<string, float> NoParameters = new();

		public static void Play(AudioEvent audioEvent)
		{
			if (!audioEvent) return;

			if (EditorApplication.isPlaying)
			{
				if (AudioManager.Instance) AudioManager.Instance.PlayOneShot(audioEvent, AudioManager.Instance.transform.position);
				return;
			}

			if (audioEvent.FmodEvent.IsNull)
			{
				Debug.LogWarning($"{nameof(AudioPreview)}: {audioEvent.name} has no FMOD event.", audioEvent);
				return;
			}

			var eventRef = EventManager.EventFromGUID(audioEvent.FmodEvent.Guid);
			if (!eventRef)
			{
				Debug.LogWarning($"{nameof(AudioPreview)}: {audioEvent.name}'s FMOD event is not in the built banks. Rebuild them in FMOD Studio, then FMOD > Refresh Banks.", audioEvent);
				return;
			}

			EditorUtils.LoadPreviewBanks();
			EditorUtils.PreviewEvent(eventRef, NoParameters, audioEvent.Volume);
		}

		public static void StopAll() => EditorUtils.StopAllPreviews();
	}
}
