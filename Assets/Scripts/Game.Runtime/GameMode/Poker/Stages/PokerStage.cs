using System.Collections.Generic;
using Game.Runtime.GameMode.Config;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Stages
{
	// A stage is an asset, not a scene object. The round is a list of them on a PokerStageSequence, so
	// swapping that one reference swaps the whole shape of the game without touching the table prefab,
	// and every stage carries the numbers it runs on instead of reaching for a shared rule asset.
	//
	// The mode runs a clone of each asset, which is what lets a stage keep runtime state without the
	// edited asset following it into the next play session.
	public abstract class PokerStage : ScriptableObject
	{
		[Header("Stage")]
		[Tooltip("Replicated to clients so UI can key off the running stage without knowing the type. Empty falls back to the asset name.")]
		[SerializeField] private string _stageId;

		[Tooltip("Seconds the table holds on this stage after it finishes, before the next one opens. Without it a street can end and the next begin in the same frame, and nobody sees what happened.")]
		[SerializeField] private float _exitDelay = 0.75f;

		public string StageId => string.IsNullOrEmpty(_stageId) ? name : _stageId;
		public float ExitDelay => Mathf.Max(0f, _exitDelay);

		public PokerGameMode GameMode { get; private set; }
		public bool IsRunning { get; private set; }
		public bool IsPaused { get; private set; }

		protected PokerGameData Data => GameMode ? GameMode.Data : null;

		public void Initialize(PokerGameMode gameMode)
		{
			GameMode = gameMode;
			OnInitialize();
		}

		public void DeInitialize()
		{
			OnDeInitialize();
			GameMode = null;
		}

		// What this stage offers the host to tune. Each declares under its own StageId, which is how a
		// sequence of four near-identical street assets still comes out as four sections in the panel.
		public void CollectConfigEntries(List<MatchConfigEntry> entries) => OnCollectConfigEntries(entries);

		protected virtual void OnCollectConfigEntries(List<MatchConfigEntry> entries) { }

		// A clock that ran out during the pause gets a moment on resume rather than expiring on the
		// first tick — the player it belongs to spent that time watching an overlay, not thinking.
		private const float MinimumResumedSeconds = 1f;

		private bool _exiting;
		private float _exitRemaining;
		private PokerStage _exitTarget;

		private bool _hasPausedStageTimer;
		private float _pausedStageTimerRemaining;

		public void StartStage()
		{
			IsRunning = true;
			IsPaused = false;

			_exiting = false;
			_exitTarget = null;
			_hasPausedStageTimer = false;

			OnStartStage();
		}

		public void EndStage()
		{
			if (!IsRunning) return;

			IsRunning = false;
			IsPaused = false;
			OnEndStage();
		}

		public void PauseStage()
		{
			if (!IsRunning || IsPaused) return;

			IsPaused = true;

			// The stage clock is an absolute server time, so left alone it would drain away under an
			// overlay. It is taken down here and handed back on resume — every stage that runs one
			// wants the same thing, so the base does it once rather than each subclass remembering to.
			_hasPausedStageTimer = Data && Data.HasStageTimer;
			if (_hasPausedStageTimer)
			{
				_pausedStageTimerRemaining = Data.StageTimeRemaining;
				GameMode.ClearStageTimer();
			}

			OnPauseStage();
		}

		public void ResumeStage()
		{
			if (!IsRunning || !IsPaused) return;

			IsPaused = false;

			if (_hasPausedStageTimer)
			{
				_hasPausedStageTimer = false;
				GameMode.BeginStageTimer(Mathf.Max(_pausedStageTimerRemaining, MinimumResumedSeconds));
			}

			OnResumeStage();
		}

		public void TickStage(float deltaTime)
		{
			if (!IsRunning || IsPaused) return;

			// Once the stage has called it a day nothing else should run — it is only still here so the
			// table can be looked at before the next one takes over.
			if (_exiting)
			{
				_exitRemaining -= deltaTime;
				if (_exitRemaining <= 0f) CompleteExit();

				return;
			}

			OnTickStage(deltaTime);
		}

		public virtual bool HandleAction(ulong clientId, PokerActionType action, int amount) => false;

		// A player left the table mid stage. The seat index comes along because the player object may
		// already be gone by the time this runs, and a stage that was waiting on them needs to know
		// where in the order the hole is.
		public virtual void HandlePlayerLeft(ulong clientId, int seatIndex) { }

		protected virtual void OnInitialize() { }
		protected virtual void OnDeInitialize() { }
		protected abstract void OnStartStage();
		protected virtual void OnEndStage() { }
		protected virtual void OnTickStage(float deltaTime) { }
		protected virtual void OnPauseStage() { }
		protected virtual void OnResumeStage() { }

		protected void NextStage()
		{
			if (GameMode) GameMode.NextStage();
		}

		// How a stage bows out. Passing a target jumps there instead of following the sequence — a hand
		// that ended early, a showdown returning to the idle table. The wait is the stage's own affair,
		// so a stage that wants to hand over instantly just leaves its delay at zero.
		protected void FinishStage(PokerStage target = null)
		{
			if (_exiting) return;

			_exitTarget = target;

			if (ExitDelay <= 0f)
			{
				CompleteExit();
				return;
			}

			_exiting = true;
			_exitRemaining = ExitDelay;
		}

		private void CompleteExit()
		{
			_exiting = false;

			var target = _exitTarget;
			_exitTarget = null;

			if (target && GameMode) GameMode.GoToStage(target);
			else NextStage();
		}
	}
}
