using Game.Runtime.GameMode.Poker.Modules;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Stages
{
	// The moment an accusation lands: pushed on top of whatever street was running, it freezes the
	// table and plays out in two acts — first the accusation hangs in the air while everyone gets to
	// look at each other, then the verdict falls and stays up long enough to be read. Only after both
	// does the hand resume exactly where it left off. An overlay, not a sequence stage — it never
	// takes a slot in the loop.
	[CreateAssetMenu(fileName = "PokerStage_Report", menuName = "Game/Poker/Stages/Report")]
	public class PokerReportStage : PokerStage
	{
		[Header("Timing")]
		[Tooltip("Seconds the accusation stands before the verdict is revealed — the table's time to sweat, argue and think.")]
		[SerializeField] private float _thinkingDuration = 5f;

		[Tooltip("Seconds the verdict stays on screen before the hand resumes.")]
		[SerializeField] private float _verdictDuration = 3f;

		private PokerAbilityModule _module;
		private bool _verdictResolved;

		protected override void OnStartStage()
		{
			_module = FindAbilityModule();
			_verdictResolved = false;

			// No thinking time configured — the verdict falls the moment the report lands.
			if (_thinkingDuration <= 0f)
			{
				ResolveVerdict();
				return;
			}

			GameMode.BeginStageTimer(_thinkingDuration);
		}

		protected override void OnEndStage() => GameMode.ClearStageTimer();

		protected override void OnTickStage(float deltaTime)
		{
			if (!GameMode.IsStageTimerExpired()) return;

			if (!_verdictResolved)
			{
				ResolveVerdict();
				return;
			}

			// Overlays leave by popping — FinishStage would walk the sequence and skip the street
			// this one interrupted.
			GameMode.PopOverlay();
		}

		private void ResolveVerdict()
		{
			_verdictResolved = true;

			if (_module) _module.ResolvePendingReportServer();

			GameMode.BeginStageTimer(Mathf.Max(0.1f, _verdictDuration));
		}

		private PokerAbilityModule FindAbilityModule()
		{
			foreach (var module in GameMode.Modules)
			{
				if (module is PokerAbilityModule abilityModule) return abilityModule;
			}

			return null;
		}
	}
}
