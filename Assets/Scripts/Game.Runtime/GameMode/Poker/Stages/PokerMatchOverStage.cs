using System;
using System.Threading;
using Game.Runtime.GameMode.Poker.Hallucination;
using Game.Runtime.GameMode.Poker.Player;
using Game.Runtime.Utility;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Serialization;

namespace Game.Runtime.GameMode.Poker.Stages
{
	// The match has one player left in it (or none). Every screen blinks into the survivor dancing on the
	// table, the camera comes round to their face, the table names them, then it blinks again and puts
	// everything back behind the closed eye — blood, hallucination, what was eaten, the pot, the cards — so the idle table
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
			Settling,
			IntroClosing,
			IntroShut,
			Dancing,
			Announcing,
			Closing,
			Done
		}

		[Header("Timing")]
		[Tooltip("Seconds after the last roll and death still playing out (every player's ServerOutcomeRemaining) before the survivor is announced. Two players going under at once both finish falling first; the margin covers the deaths reaching every screen a little after the server.")]
		[MinValue(0f)]
		[SerializeField] private float _settleDuration = 1f;

		[Tooltip("Seconds the eye stays shut after the intro blink closes, while the survivor is lifted onto the table.")]
		[MinValue(0f)]
		[SerializeField] private float _introHoldDuration = 0.3f;

		[Tooltip("Seconds the wide shot of the survivor dancing on the table holds before the close-up.")]
		[FormerlySerializedAs("_orbitDuration")]
		[MinValue(0f)]
		[SerializeField] private float _danceDuration = 4f;

		[Tooltip("Seconds the survivor is announced before the table blinks.")]
		[MinValue(0f)]
		[SerializeField] private float _announceDuration = 4f;

		[Tooltip("The eyelids every screen shuts with: the same blink video a rung change uses. Its close is when the reset is written; its open is how long the eye takes to open again.")]
		[Required]
		[SerializeField] private PokerHallucinationPacing _blink;

		[Tooltip("Seconds the eye stays shut after the reset is written and the idle stage has opened. Long enough for the reset to reach every client, for the hallucination blink it sets off to close and hold (PokerHallucinationController close + hold), and for the idle shot to turn the view ahead — all behind the black.")]
		[MinValue(0f)]
		[SerializeField] private float _blinkHoldDuration = 0.8f;

		[Header("References")]
		[Tooltip("Where the table goes once everything is back — the waiting room, where the host starts the next match.")]
		[Required]
		[SerializeField] private PokerStage _idleStage;

		private Step _step;
		private float _timer;

		public PokerHallucinationPacing Blink => _blink;

		private float BlinkCloseDuration => _blink ? _blink.CloseDuration : 0f;

		protected override void OnStartStage()
		{
			GameMode.ClearTurn();
			Data.Showdown.Clear();
			Data.MatchResetting.Value = false;
			Data.VictoryShot.Value = PokerVictoryShot.None;

			_step = Step.Settling;
			_timer = _settleDuration;
		}

		protected override void OnTickStage(float deltaTime)
		{
			// A death still playing out holds the announcement back, however the match was decided.
			if (_step == Step.Settling && AnyOutcomePlaying()) _timer = _settleDuration;

			_timer -= deltaTime;
			if (_timer > 0f) return;

			switch (_step)
			{
				case Step.Settling:
					Data.SurvivorClientId.Value = FindSurvivor();

					// Nobody left standing has nobody to dance: the table just says so.
					if (Data.SurvivorClientId.Value == PokerGameData.NoTurn)
					{
						Announce();
						break;
					}

					Data.MatchResetting.Value = true;
					_step = Step.IntroClosing;
					_timer = BlinkCloseDuration;
					break;

				// Named only once the eye is shut, so the cut and the lift onto the table land behind the black on
				// every screen; a screen that hears it early still waits out its own close (PokerVictoryVisual).
				case Step.IntroClosing:
					Data.VictoryShot.Value = PokerVictoryShot.Wide;
					SetSurvivorDancing(true);
					Data.Phase.Value = PokerPhase.MatchOver;
					_step = Step.IntroShut;
					_timer = _introHoldDuration;
					break;

				case Step.IntroShut:
					Data.MatchResetting.Value = false;
					_step = Step.Dancing;
					_timer = _danceDuration;
					break;

				case Step.Dancing:
					Data.VictoryShot.Value = PokerVictoryShot.CloseUp;
					Announce();
					break;

				case Step.Announcing:
					Data.MatchResetting.Value = true;
					_step = Step.Closing;
					_timer = BlinkCloseDuration;
					break;

				// The table is put back and the idle stage opens while every screen is still shut, so whatever the
				// reset sets moving — the idle shot turning the view ahead, a pose settling, an effect easing out —
				// does it behind the black. The eye opens a hold later, from a wait that outlives this stage.
				case Step.Closing:
					ResetTable();
					_step = Step.Done;
					_ = OpenAfterHold(Data, GameMode.destroyCancellationToken);
					FinishStage(_idleStage);
					break;
			}
		}

		private void Announce()
		{
			Data.Phase.Value = PokerPhase.MatchOver;
			_step = Step.Announcing;
			_timer = _announceDuration;
		}

		// Cut short before the reset, the screens must not stay shut and the survivor must come down. After
		// it, the hold below opens them.
		protected override void OnEndStage()
		{
			if (_step == Step.Done) return;

			Data.MatchResetting.Value = false;
			EndVictory();
		}

		private void EndVictory()
		{
			SetSurvivorDancing(false);
			Data.VictoryShot.Value = PokerVictoryShot.None;
		}

		private void SetSurvivorDancing(bool dancing)
		{
			var survivor = PokerPlayer.Find(Data.SurvivorClientId.Value);
			if (survivor && survivor.WinnerPose) survivor.WinnerPose.ServerSetDancing(dancing);
		}

		private async Awaitable OpenAfterHold(PokerGameData data, CancellationToken ct)
		{
			try
			{
				await AwaitableUtility.WaitUnscaledAsync(_blinkHoldDuration, ct);
			}
			catch (OperationCanceledException)
			{
				return;
			}

			if (data) data.MatchResetting.Value = false;
		}

		private void ResetTable()
		{
			EndVictory();
			GameMode.EndGame();
			GameMode.ServerResetMatchStats();
			PokerTableUtility.ResetPot(Data);

			Data.SurvivorClientId.Value = PokerGameData.NoTurn;
		}

		private static bool AnyOutcomePlaying()
		{
			foreach (var player in PokerPlayer.All)
			{
				if (player && player.HallucinationRoll && player.HallucinationRoll.ServerOutcomeRemaining > 0f) return true;
			}

			return false;
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
