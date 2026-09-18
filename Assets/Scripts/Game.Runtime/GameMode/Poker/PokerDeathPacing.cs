using UnityEngine;

namespace Game.Runtime.GameMode.Poker
{
	// Going under, in one place. The shot, the fall and the head going are one moment seen from three
	// components, and the eating beat waits for all of it before anything else happens at the table.
	// Everything counts from the blink the crossing sets off having opened again, which the player's
	// hallucination controller answers.
	[CreateAssetMenu(fileName = "PokerDeathPacing", menuName = "Game/Poker/Death Pacing")]
	public class PokerDeathPacing : ScriptableObject
	{
		[Tooltip("Seconds between the death shot going up and the fall starting, so the camera has arrived before the body moves.")]
		[field: SerializeField, Min(0f)] public float PoseDelay { get; private set; } = 0.4f;

		[Tooltip("Seconds after the fall starts before the head goes — the frame the death animation's shaking ends.")]
		[field: SerializeField, Min(0f)] public float HeadVanishDelay { get; private set; } = 2.5f;

		[Tooltip("Seconds the whole room watches the body, from the shot going up.")]
		[field: SerializeField, Min(0f)] public float ShotDuration { get; private set; } = 4f;

		// Counted from the shot going up: how long until nothing more about this death is left to see.
		public float BeatDuration => Mathf.Max(ShotDuration, PoseDelay + HeadVanishDelay);
	}
}
