using Game.Runtime.Audio;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Visual
{
	// What the table sounds like on every screen: a turn coming round is heard as it reaches each player.
	// The music's beat is the gameplay track's own driver (PokerAmbienceParameterDriver).
	public class PokerTableAudioVisual : PokerVisual
	{
		[Tooltip("Played as the turn reaches a player. Empty: turns pass in silence.")]
		[SerializeField] private AudioEvent _turnSound;

		protected override void OnBind() => Data.CurrentTurnClientId.OnValueChanged += HandleTurnChanged;

		protected override void OnUnbind()
		{
			if (Data) Data.CurrentTurnClientId.OnValueChanged -= HandleTurnChanged;
		}

		private void HandleTurnChanged(ulong previous, ulong current)
		{
			if (current == PokerGameData.NoTurn || current == previous) return;
			if (_turnSound && AudioManager.Instance) AudioManager.Instance.PlayOneShot(_turnSound, transform.position);
		}
	}
}
