using Game.Runtime.Audio;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Visual
{
	// What the table sounds like on every screen: the ambience follows the round's phase (waiting, playing,
	// eating), and a turn coming round is heard as it reaches each player.
	public class PokerTableAudioVisual : PokerVisual
	{
		private const float WaitingLevel = 0f;
		private const float PlayingLevel = 1f;
		private const float EatingLevel = 2f;

		[Tooltip("Played as the turn reaches a player. Empty: turns pass in silence.")]
		[SerializeField] private AudioEvent _turnSound;

		protected override void OnBind()
		{
			Data.Phase.OnValueChanged += HandlePhaseChanged;
			Data.CurrentTurnClientId.OnValueChanged += HandleTurnChanged;

			ApplyAmbience(Data.Phase.Value);
		}

		protected override void OnUnbind()
		{
			if (!Data) return;

			Data.CurrentTurnClientId.OnValueChanged -= HandleTurnChanged;
			Data.Phase.OnValueChanged -= HandlePhaseChanged;
		}

		private void HandlePhaseChanged(PokerPhase previous, PokerPhase current) => ApplyAmbience(current);

		private static void ApplyAmbience(PokerPhase phase)
		{
			if (MusicController.Instance) MusicController.Instance.SetAmbienceLevel(AmbienceLevel(phase));
		}

		private static float AmbienceLevel(PokerPhase phase) => phase switch
		{
			PokerPhase.Waiting or PokerPhase.Finished or PokerPhase.MatchOver => WaitingLevel,
			PokerPhase.Eating => EatingLevel,
			_ => PlayingLevel
		};

		private void HandleTurnChanged(ulong previous, ulong current)
		{
			if (current == PokerGameData.NoTurn || current == previous) return;
			if (_turnSound && AudioManager.Instance) AudioManager.Instance.PlayOneShot(_turnSound, transform.position);
		}
	}
}
