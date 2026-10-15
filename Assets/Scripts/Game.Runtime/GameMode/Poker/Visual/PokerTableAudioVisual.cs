using Game.Runtime.Audio;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Visual
{
	// What the table sounds like on every screen: a match starting, and a turn coming round as it reaches each
	// player. The music's beat is the gameplay track's own driver (PokerAmbienceParameterDriver).
	public class PokerTableAudioVisual : PokerVisual
	{
		[Tooltip("Played as a match starts (its first hand, not every hand). Empty: silent.")]
		[SerializeField] private AudioEvent _matchStartSound;

		[Tooltip("Played as the turn reaches a player. Empty: turns pass in silence.")]
		[SerializeField] private AudioEvent _turnSound;

		private PokerNoticeChannel _notices;

		protected override void OnBind()
		{
			Data.CurrentTurnClientId.OnValueChanged += HandleTurnChanged;

			_notices = GameMode.Notices;
			if (_notices) _notices.OnNotice += HandleNotice;
		}

		protected override void OnUnbind()
		{
			if (_notices) _notices.OnNotice -= HandleNotice;
			_notices = null;

			if (Data) Data.CurrentTurnClientId.OnValueChanged -= HandleTurnChanged;
		}

		private void HandleNotice(PokerNotice notice)
		{
			if (notice.Kind != PokerNoticeKind.MatchStarted) return;
			if (_matchStartSound && AudioManager.Instance) AudioManager.Instance.PlayOneShot(_matchStartSound, transform.position);
		}

		private void HandleTurnChanged(ulong previous, ulong current)
		{
			if (current == PokerGameData.NoTurn || current == previous) return;
			if (_turnSound && AudioManager.Instance) AudioManager.Instance.PlayOneShot(_turnSound, transform.position);
		}
	}
}
