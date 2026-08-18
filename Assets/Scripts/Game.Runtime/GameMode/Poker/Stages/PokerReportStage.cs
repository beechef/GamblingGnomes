using Game.Runtime.GameMode.Poker.Abilities;
using Game.Runtime.GameMode.Poker.Modules;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Stages
{
	// The accusation, from the arm going out to the verdict landing. Pushed on top of whatever street was
	// running, it freezes the table: the accuser stands with a finger out and looks for a face, the one
	// they settle on answers for it in blood, and then the table finds out.
	//
	// Naming somebody is the accuser's whole move — there is no menu and no taking it back. The accused
	// cannot refuse to answer, only decide what the answer is worth: match what is on the table, or shove.
	// Shoving is the one thing that hands the question back, and the accuser may then walk away and leave
	// what they staked — which is the whole risk in accusing, and the whole hope in shoving.
	//
	// An overlay, not a sequence stage: it never takes a slot in the loop.
	[CreateAssetMenu(fileName = "PokerStage_Report", menuName = "Game/Poker/Stages/Report")]
	public class PokerReportStage : PokerStage
	{
		[Header("Timing")]
		[Tooltip("Seconds the accuser gets to find a face. Long enough to look round the whole table, short enough that everyone else is watching it happen rather than waiting for it.")]
		[SerializeField] private float _aimDuration = 10f;

		[Tooltip("Seconds the accused gets to answer. Running out matches what is already on the table — saying nothing is not a way out of answering.")]
		[SerializeField] private float _responseDuration = 15f;

		[Tooltip("Seconds between the last answer and the verdict. Nothing happens here — that is the point: a result landing on the same frame as the button press is one nobody watched arrive.")]
		[SerializeField] private float _judgingDuration = 5f;

		[Tooltip("Seconds the verdict stays on screen before the hand resumes.")]
		[SerializeField] private float _verdictDuration = 3f;

		// Read by the notice announcing the verdict, so the card and the pause are the same length by
		// construction rather than by two numbers being kept in step by hand.
		public float VerdictDuration => Mathf.Max(0.1f, _verdictDuration);

		private PokerAbilityModule _module;
		private PokerReportPhase _phase;
		private ulong _accuserClientId;
		private ulong _targetClientId;
		private int _pendingStake;

		// Which of the two the Response beat is waiting on. The phase says the accusation is being answered;
		// this says whose answer, because a shove hands the question back across the table.
		private bool _awaitingAccuser;

		// Which answer the pause is sitting on top of.
		private bool _conceded;

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
			_pendingStake = 0;
			_conceded = false;
			_awaitingAccuser = false;

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

					// Silence from the accused answers with what is already on the table, which is the least
					// they can be in for anyway. Silence from the accuser walks away from a shove, the way
					// saying nothing backs down everywhere else at this table.
					if (_awaitingAccuser) Concede();
					else Answer(_module.ReportStake.Value, PokerActionType.Call);

					return;

				case PokerReportPhase.Judging:
					if (!GameMode.IsStageTimerExpired()) return;

					Judge();
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
			if (Data.CurrentTurnClientId.Value != clientId) return false;

			// Being asked to stand behind a shove: pay to see it, or walk away and leave what was already
			// staked. Shoving back is not on offer — the accuser has already said what they think it is
			// worth, and raising their own accusation would be answering nobody.
			if (_awaitingAccuser)
			{
				if (clientId != _accuserClientId) return false;

				if (action == PokerActionType.Call) return AccuserCall();
				if (action == PokerActionType.Fold) return Concede();

				return false;
			}

			if (clientId != _targetClientId) return false;

			var accuser = GameMode.FindSeatedPlayer(_accuserClientId);
			var accused = GameMode.FindSeatedPlayer(_targetClientId);

			// Match it or shove it. Folded out of the hand or not, the accused still has to answer for the
			// accusation — so both of their answers are a number.
			if (action == PokerActionType.Call) return Answer(_module.ReportStake.Value, PokerActionType.Call);
			if (action == PokerActionType.AllIn) return Answer(_module.AllInStake(accuser, accused), PokerActionType.AllIn);

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

			_awaitingAccuser = false;
			SetPhase(PokerReportPhase.Response);
			GameMode.BeginTurn(_targetClientId, _responseDuration);
		}

		// The accused's answer. Matching what is already on the table settles it there and then; shoving
		// hands the question back, because the accuser is now being asked for more than they put up.
		private bool Answer(int accusedStake, PokerActionType action)
		{
			_module.AnnounceReportActionServer(_targetClientId, action, accusedStake);

			if (!_module.RequiresAccuserCall(accusedStake)) return Resolve(accusedStake);

			_pendingStake = accusedStake;
			_awaitingAccuser = true;

			// The number on the table is now the shove, so the bar offers to match the right one — the same
			// value the server will clamp against when the call arrives.
			_module.SetReportStakeServer(accusedStake);

			GameMode.BeginTurn(_accuserClientId, _responseDuration);

			return true;
		}

		// Both answers end the asking and start the waiting. What was decided is held here rather than acted
		// on, because the moment the module resolves it the guilt is published — and the pause exists to sit
		// between the last button press and that.
		private bool Resolve(int accusedStake)
		{
			_pendingStake = accusedStake;

			return BeginJudging(false);
		}

		// The accuser standing behind what they said, once a shove has asked them to.
		private bool AccuserCall()
		{
			_module.AnnounceReportActionServer(_accuserClientId, PokerActionType.Call, _pendingStake);

			return Resolve(_pendingStake);
		}

		private bool Concede()
		{
			_module.AnnounceReportActionServer(_accuserClientId, PokerActionType.Fold, 0);

			return BeginJudging(true);
		}

		private bool BeginJudging(bool conceded)
		{
			GameMode.ClearTurn();

			_conceded = conceded;

			SetPhase(PokerReportPhase.Judging);
			GameMode.BeginStageTimer(Mathf.Max(0.1f, _judgingDuration));

			return true;
		}

		private void Judge()
		{
			if (_conceded) _module.ConcedeReportServer();
			else _module.ResolveReportServer(_pendingStake);

			BeginVerdict();
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
