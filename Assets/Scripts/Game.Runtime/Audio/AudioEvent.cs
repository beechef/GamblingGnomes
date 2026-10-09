using FMODUnity;
using UnityEngine;

namespace Game.Runtime.Audio
{
	// One sound the game can ask for. Callers and cues name this asset, never the FMOD event, so the
	// backend behind AudioManager can change without touching them.
	[CreateAssetMenu(fileName = "AudioEvent", menuName = "Game/Audio/Audio Event")]
	public class AudioEvent : ScriptableObject
	{
		[field: SerializeField] public EventReference FmodEvent { get; private set; }

		[field: SerializeField, Range(0f, 1f)] public float Volume { get; private set; } = 1f;
	}
}
