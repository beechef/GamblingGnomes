using Game.Runtime.GameMode.Poker.Modules;
using Game.Runtime.GameMode.Poker.Player;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Stages
{
	// The moment an accusation lands: pushed on top of whatever street was running, it freezes the table
	// and plays out as a duel between the two of them. The accused answers first, and answering is not
	// optional — they name what they are willing to stake on being clean, or shove the lot. Then the
	// accuser either pays to see it or backs down and lets it go.
	//
	// Only a challenge that was actually called gets judged, which is what makes a big number worth
	// putting up when you are guilty. An overlay, not a sequence stage: it never takes a slot in the loop.
	[CreateAssetMenu(fileName = "PokerStage_Report", menuName = "Game/Poker/Stages/Report")]
	public class PokerReportStage : PokerStage
	{
		private enum ReportPhase
		{
			Defence,
			Response,
			Verdict
		}

		[Header("Stake")]
		[Tooltip("Smallest the accused may put up. They cannot decline the challenge, so this is the floor rather than an opening bid — a wallet too short for it stakes whatever is left.")]
		[SerializeField] private int _minimumStake = 50;

		[Header("Timing")]
		[Tooltip("Seconds the accused gets to name their stake. Running out puts up the floor: saying nothing is not a way out of answering. Zero or less leaves them unhurried.")]
		[SerializeField] private float _defenceDuration = 20f;

		[Tooltip("Seconds the accuser gets to answer. Running out backs down, the way silence does everywhere else at this table.")]
		[SerializeField] private float _responseDuration = 20f;

		[Tooltip("Seconds the verdict stays on screen before the hand resumes.")]
		[SerializeField] private float _verdictDuration = 3f;

		public int MinimumStake => Mathf.Max(0, _minimumStake);

		private PokerAbilityModule _module;
		private ReportPhase _phase;
		private ulong _accuserClientId;
		private ulong _targetClientId;
		private int _stake;

		// Both of them have to be able to cover the number or a call would not be a call, so the ceiling is
		// the smaller of the two wallets. The floor gives way to it rather than the other way round: a short
		// stack stakes what it has. Public because the bar offering the number runs the same maths — one
		// source of it, so the client can never offer what the server will refuse.
		public int StakeCeiling(PokerPlayer accused, PokerPlayer accuser)
		{
			var accusedChips = accused && accused.Data ? accused.Data.Chips : 0;
			var accuserChips = accuser && accuser.Data ? accuser.Data.Chips : 0;

			return Mathf.Max(0, Mathf.Min(accusedChips, accuserChips));
		}

		public int StakeFloor(int ceiling) => Mathf.Min(MinimumStake, ceiling);

		public int ClampStake(int amount, int ceiling) => Mathf.Clamp(amount, StakeFloor(ceiling), ceiling);

		protected override void OnStartStage()
		{
			_module = GameMode.FindModule<PokerAbilityModule>();
			_stake = 0;

			// Nothing left to try: the report settled itself on the way in, or there is no module to ask.
			if (_module == null || !_module.HasPendingReport)
			{
				BeginVerdict();
				return;
			}

			_accuserClientId = _module.PendingAccuserClientId;
			_targetClientId = _module.PendingTargetClientId;

			BeginDefence();
		}

		protected override void OnEndStage()
		{
			GameMode.ClearTurn();
			GameMode.ClearStageTimer();
		}

		protected override void OnTickStage(float deltaTime)
		{
			switch (_phase)
			{
				case ReportPhase.Defence:
					if (!GameMode.IsTurnExpired()) return;

					SubmitDefence(MinimumStake);
					return;

				case ReportPhase.Response:
					if (!GameMode.IsTurnExpired()) return;

					SubmitResponse(false);
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
			if (Data.CurrentTurnClientId.Value != clientId) return false;

			switch (_phase)
			{
				case ReportPhase.Defence:
					if (clientId != _targetClientId) return false;

					// Folded out of the hand or not, the accused still has to answer for the accusation —
					// so their side of this is a number, and the only choice is how big.
					if (action == PokerActionType.AllIn) return SubmitDefence(int.MaxValue);
					if (action == PokerActionType.Bet) return SubmitDefence(amount);

					return false;

				case ReportPhase.Response:
					if (clientId != _accuserClientId) return false;

					if (action == PokerActionType.Call) return SubmitResponse(true);
					if (action == PokerActionType.Fold) return SubmitResponse(false);

					return false;

				default:
					return false;
			}
		}

		// Either of them walking out ends it unjudged. There is no wallet left to take from and no hand left
		// to answer for, so the accusation is let go rather than settled against somebody who has gone.
		public override void HandlePlayerLeft(ulong clientId, int seatIndex)
		{
			if (!IsRunning || IsPaused) return;
			if (_phase == ReportPhase.Verdict) return;
			if (clientId != _accuserClientId && clientId != _targetClientId) return;

			Drop();
		}

		private void BeginDefence()
		{
			_phase = ReportPhase.Defence;

			// Given by name rather than by whether they could act on a street: a player who folded out of
			// the hand can still be accused, and still has to answer.
			if (!GameMode.FindSeatedPlayer(_targetClientId))
			{
				Drop();
				return;
			}

			GameMode.BeginTurn(_targetClientId, _defenceDuration);
		}

		private bool SubmitDefence(int amount)
		{
			var accused = GameMode.FindSeatedPlayer(_targetClientId);
			var accuser = GameMode.FindSeatedPlayer(_accuserClientId);

			if (!accused || !accuser)
			{
				Drop();
				return true;
			}

			_stake = ClampStake(amount, StakeCeiling(accused, accuser));
			_module.ReportStake.Value = _stake;

			_phase = ReportPhase.Response;
			GameMode.BeginTurn(_accuserClientId, _responseDuration);

			return true;
		}

		private bool SubmitResponse(bool called)
		{
			GameMode.ClearTurn();

			_module.ResolvePendingReportServer(called ? _stake : 0, called);
			BeginVerdict();

			return true;
		}

		private void Drop()
		{
			if (_module != null) _module.ResolvePendingReportServer(0, false);

			BeginVerdict();
		}

		private void BeginVerdict()
		{
			_phase = ReportPhase.Verdict;

			GameMode.ClearTurn();
			GameMode.BeginStageTimer(Mathf.Max(0.1f, _verdictDuration));
		}
	}
}
