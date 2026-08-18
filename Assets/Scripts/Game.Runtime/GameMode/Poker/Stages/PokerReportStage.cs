using Game.Runtime.GameMode.Poker.Abilities;
using Game.Runtime.GameMode.Poker.Modules;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Stages
{
	// The accusation, from the arm going out to the verdict landing. Pushed on top of whatever street was
	// running, it freezes the table and plays out in three beats: the accuser stands with a finger out and
	// looks for a face, the one they settle on answers for it in blood, and then the table finds out.
	//
	// Naming somebody is the accuser's whole move — there is no menu and no taking it back. Answering is
	// the accused's, and the only choice they have is how much the answer is worth. An overlay, not a
	// sequence stage: it never takes a slot in the loop.
	[CreateAssetMenu(fileName = "PokerStage_Report", menuName = "Game/Poker/Stages/Report")]
	public class PokerReportStage : PokerStage
	{
		[Header("Timing")]
		[Tooltip("Seconds the accuser gets to find a face. Long enough to look round the whole table, short enough that everyone else is watching it happen rather than waiting for it.")]
		[SerializeField] private float _aimDuration = 10f;

		[Tooltip("Seconds the accused gets to answer. Running out matches what is already on the table — saying nothing is not a way out of answering.")]
		[SerializeField] private float _responseDuration = 15f;

		[Tooltip("Seconds the verdict stays on screen before the hand resumes.")]
		[SerializeField] private float _verdictDuration = 3f;

		private PokerAbilityModule _module;
		private PokerReportPhase _phase;
		private ulong _accuserClientId;
		private ulong _targetClientId;

		protected override void OnStartStage()
		{
			_module = GameMode.FindModule<PokerAbilityModule>();

			// Nothing left to try: the report settled itself on the way in, or there is no module to ask.
			// Worth a word either way — an overlay that opens straight onto its own verdict looks from the
			// outside like the accusation never started.
			if (_module == null || !_module.HasPendingReport)
			{
				Debug.LogWarning($"[PokerReportStage] Opened with nothing to judge ({(_module == null ? "no ability module on the mode" : "no pending report")}).");

				BeginVerdict();
				return;
			}

			_accuserClientId = _module.PendingAccuserClientId;
			_targetClientId = _accuserClientId;

			BeginAiming();
		}

		protected override void OnEndStage()
		{
			GameMode.ClearTurn();
			GameMode.ClearStageTimer();

			if (_module != null) _module.SetReportPhaseServer(PokerReportPhase.None);
		}

		protected override void OnTickStage(float deltaTime)
		{
			switch (_phase)
			{
				case PokerReportPhase.Aiming:
					if (!GameMode.IsTurnExpired()) return;

					LockTarget();
					return;

				case PokerReportPhase.Response:
					if (!GameMode.IsTurnExpired()) return;

					// Silence answers with what is already on the table, which is the least the accused can
					// be in for anyway.
					Resolve(_module.ReportStake.Value);
					return;

				default:
					if (!GameMode.IsStageTimerExpired()) return;

					// Overlays leave by popping — FinishStage would walk the sequence and skip the street
					// this one interrupted.
					GameMode.PopOverlay();
					return;
			}
		}

		public override bool HandleAction(ulong clientId, PokerActionType action, int amount)
		{
			if (!IsRunning || IsPaused) return false;
			if (_phase != PokerReportPhase.Response) return false;
			if (Data.CurrentTurnClientId.Value != clientId || clientId != _targetClientId) return false;

			var accuser = GameMode.FindSeatedPlayer(_accuserClientId);
			var accused = GameMode.FindSeatedPlayer(_targetClientId);

			// Match it or shove it. Folded out of the hand or not, the accused still has to answer for the
			// accusation — so both of their answers are a number.
			if (action == PokerActionType.Call) return Resolve(_module.ReportStake.Value);
			if (action == PokerActionType.AllIn) return Resolve(_module.AllInStake(accuser, accused));

			return false;
		}

		// Either of them walking out ends it unjudged. There is no blood left to take and no hand left to
		// answer for, so the accusation is let go rather than settled against somebody who has gone.
		public override void HandlePlayerLeft(ulong clientId, int seatIndex)
		{
			if (!IsRunning || IsPaused) return;
			if (_phase == PokerReportPhase.Verdict) return;
			if (clientId != _accuserClientId && clientId != _targetClientId) return;

			Drop();
		}

		private void BeginAiming()
		{
			SetPhase(PokerReportPhase.Aiming);

			// The arm goes out as the stage opens, not as the button was pressed: by now the table is
			// stopped and everyone is watching.
			_module.BeginAimServer();

			// The turn is what says whose move this is, and aiming is a move: the accuser's client reads it
			// to know that their looking is now being watched.
			GameMode.BeginTurn(_accuserClientId, _aimDuration);
		}

		private void LockTarget()
		{
			if (!_module.LockReportTargetServer())
			{
				// Pointed at nobody. The accuser is let off rather than charged for an accusation the table
				// never heard — and the report has to be closed out, or nobody could ever file another.
				Drop();
				return;
			}

			_targetClientId = _module.PendingTargetClientId;

			SetPhase(PokerReportPhase.Response);
			GameMode.BeginTurn(_targetClientId, _responseDuration);
		}

		private bool Resolve(int accusedStake)
		{
			GameMode.ClearTurn();

			_module.ResolveReportServer(accusedStake);
			BeginVerdict();

			return true;
		}

		private void Drop()
		{
			if (_module != null) _module.DropReportServer();

			BeginVerdict();
		}

		private void BeginVerdict()
		{
			SetPhase(PokerReportPhase.Verdict);

			GameMode.ClearTurn();
			GameMode.BeginStageTimer(Mathf.Max(0.1f, _verdictDuration));
		}

		private void SetPhase(PokerReportPhase phase)
		{
			_phase = phase;

			if (_module != null) _module.SetReportPhaseServer(phase);
		}
	}
}
