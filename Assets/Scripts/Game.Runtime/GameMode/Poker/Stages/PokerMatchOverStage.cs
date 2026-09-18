using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Stages
{
	// The match has one player left in it (or none). The table says who, blinks, and puts everything back
	// behind the closed eye — blood, hallucination, what was eaten, the pot, the cards — so the idle table
	// that opens is the one a new match starts from, not the wreck of the last one.
	//
	// The blink is replicated as a flag rather than timed on each client: every screen shuts when the flag
	// rises and opens when it falls, and the reset is written in between, so it can only ever land on a
	// screen that is already shut. The hold has to outlast the round trip to the slowest client and the
	// hallucination blink the rate dropping sets off on its own.
	[CreateAssetMenu(fileName = "PokerStage_MatchOver", menuName = "Game/Poker/Stages/Match Over")]
	public class PokerMatchOverStage : PokerStage
	{
		private enum Step
		{
			Announcing,
			Closing,
			Shut,
			Done
		}

		[Header("Timing")]
		[Tooltip("Seconds the survivor is announced before the table blinks.")]
		[MinValue(0f)]
		[SerializeField] private float _announceDuration = 4f;

		[Tooltip("Seconds every screen takes to shut. The reset is written once this has run out.")]
		[MinValue(0f)]
		[SerializeField] private float _blinkCloseDuration = 0.4f;

		[Tooltip("Seconds the eye stays shut after the reset is written. Long enough for the reset to reach every client and for the hallucination blink it sets off to finish behind it.")]
		[MinValue(0f)]
		[SerializeField] private float _blinkHoldDuration = 0.8f;

		[Tooltip("Seconds every screen takes to open again, on the idle table.")]
		[MinValue(0f)]
		[SerializeField] private float _blinkOpenDuration = 0.5f;

		[Header("References")]
		[Tooltip("Where the table goes once everything is back — the waiting room, where the host starts the next match.")]
		[Required]
		[SerializeField] private PokerStage _idleStage;

		private Step _step;
		private float _timer;

		public float BlinkCloseDuration => _blinkCloseDuration;
		public float BlinkOpenDuration => _blinkOpenDuration;

		protected override void OnStartStage()
		{
			GameMode.ClearTurn();
			Data.Showdown.Clear();

			Data.SurvivorClientId.Value = FindSurvivor();
			Data.MatchResetting.Value = false;
			Data.Phase.Value = PokerPhase.MatchOver;

			_step = Step.Announcing;
			_timer = _announceDuration;
		}

		protected override void OnTickStage(float deltaTime)
		{
			_timer -= deltaTime;
			if (_timer > 0f) return;

			switch (_step)
			{
				case Step.Announcing:
					Data.MatchResetting.Value = true;
					_step = Step.Closing;
					_timer = _blinkCloseDuration;
					break;

				case Step.Closing:
					ResetTable();
					_step = Step.Shut;
					_timer = _blinkHoldDuration;
					break;

				case Step.Shut:
					_step = Step.Done;
					Data.MatchResetting.Value = false;
					FinishStage(_idleStage);
					break;
			}
		}

		// Cut short by anything, the screens must not stay shut.
		protected override void OnEndStage()
		{
			Data.MatchResetting.Value = false;
		}

		private void ResetTable()
		{
			GameMode.EndGame();
			GameMode.ServerResetMatchStats();
			PokerTableUtility.ResetPot(Data);

			Data.SurvivorClientId.Value = PokerGameData.NoTurn;
		}

		private ulong FindSurvivor()
		{
			foreach (var player in GameMode.SeatedPlayers)
			{
				if (player && GameMode.IsPlayingThisMatch(player.Data)) return player.ClientId;
			}

			return PokerGameData.NoTurn;
		}
	}
}
