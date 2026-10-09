using Game.Runtime.Audio;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Hallucination
{
	// The roll's result as heard from the roller, on every screen, the moment the skull stops.
	public class PokerRollSoundVisual : MonoBehaviour
	{
		[Required]
		[SerializeField] private PokerHallucinationRollController _roll;

		[Tooltip("The roll stopped short of going under.")]
		[SerializeField] private AudioEvent _survivedSound;

		[Tooltip("The roll means going under: the shudder that starts the death.")]
		[SerializeField] private AudioEvent _fatalSound;

		private void OnEnable() => _roll.OnRollSettled += HandleRollSettled;

		private void OnDisable() => _roll.OnRollSettled -= HandleRollSettled;

		private void HandleRollSettled(int roll, bool fatal)
		{
			var sound = fatal ? _fatalSound : _survivedSound;
			if (sound && AudioManager.Instance) AudioManager.Instance.PlayOneShotAttached(sound, transform);
		}
	}
}
