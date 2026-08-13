using System;
using System.Collections.Generic;
using Game.Runtime.GameMode.Poker.Modules;
using Game.Runtime.GameMode.Poker.Stages;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker
{
	// The round as a machine: which stage runs, what interrupts it and what comes next. Pulled out of
	// the mode so the table's rules of motion can be read — and one day reused — without the seats,
	// wallets and RPCs that surround them there. Plain class, not a component: the mode owns exactly
	// one and its lifetime is the mode's spawn.
	//
	// Assets describe the round; this machine runs clones of them, so nothing a stage does at runtime
	// is written back into the project. Mutating entry points are only ever called on the server — the
	// mode's public wrappers carry the guard.
	public class PokerStageMachine
	{
		private readonly PokerGameMode _mode;
		private readonly Action<PokerStage> _onStageStarted;
		private readonly Action<PokerStage> _onStageEnded;

		private readonly List<PokerStage> _sources = new();
		private readonly List<PokerStage> _runtimeStages = new();

		// Stages reached by reference rather than by sequence — a module's overlay, a hand over branch.
		// They run the same way, they just never take a slot in the loop.
		private readonly List<PokerStage> _detachedStages = new();

		private readonly List<PokerStage> _overlayStack = new();
		private readonly Queue<PokerStage> _insertedStages = new();

		private int _nextStageIndex;

		public PokerStageMachine(PokerGameMode mode, Action<PokerStage> onStageStarted, Action<PokerStage> onStageEnded)
		{
			_mode = mode;
			_onStageStarted = onStageStarted;
			_onStageEnded = onStageEnded;
		}

		public PokerStage CurrentStage { get; private set; }
		public PokerStage CurrentOverlay => _overlayStack.Count > 0 ? _overlayStack[^1] : null;
		public PokerStage ActiveStage => CurrentOverlay ? CurrentOverlay : CurrentStage;

		public IReadOnlyList<PokerStage> Stages => _runtimeStages;

		// Every peer builds the same list, so a client can look a running stage up by id and read its
		// numbers — only the transitions are the server's alone.
		public void Build(PokerStageSequence sequence, IReadOnlyList<PokerModule> modules)
		{
			Release();

			_sources.Clear();

			if (sequence) sequence.CollectStages(_sources);

			foreach (var module in modules)
			{
				if (module) module.CollectStages(_sources);
			}

			foreach (var source in _sources)
			{
				if (source) _runtimeStages.Add(CreateRuntimeStage(source));
			}
		}

		public void InitializeStages()
		{
			foreach (var stage in _runtimeStages)
			{
				if (stage) stage.Initialize(_mode);
			}
		}

		public void Shutdown()
		{
			CurrentStage?.EndStage();

			for (var i = _overlayStack.Count - 1; i >= 0; i--) _overlayStack[i].EndStage();
			_overlayStack.Clear();
			_insertedStages.Clear();

			foreach (var stage in _runtimeStages)
			{
				if (stage) stage.DeInitialize();
			}

			foreach (var stage in _detachedStages)
			{
				if (stage) stage.DeInitialize();
			}

			Release();
		}

		public void Tick(float deltaTime) => ActiveStage?.TickStage(deltaTime);

		public PokerStage Find(string stageId)
		{
			if (string.IsNullOrEmpty(stageId)) return null;

			foreach (var stage in _runtimeStages)
			{
				if (stage && stage.StageId == stageId) return stage;
			}

			foreach (var stage in _detachedStages)
			{
				if (stage && stage.StageId == stageId) return stage;
			}

			return null;
		}

		// Stage fields point at project assets, so a reference has to be traded for the clone actually
		// running. Anything the sequence never mentions gets a clone of its own on first use.
		public PokerStage Resolve(PokerStage stage)
		{
			if (!stage) return null;

			var running = Find(stage.StageId);
			if (running) return running;

			var runtime = CreateRuntimeStage(stage);
			_detachedStages.Add(runtime);
			runtime.Initialize(_mode);

			return runtime;
		}

		public void Next()
		{
			CurrentStage?.EndStage();
			_onStageEnded?.Invoke(CurrentStage);

			// An inserted stage runs before the sequence resumes, and deliberately does not consume a
			// slot — when it ends the round picks up exactly where it left off.
			if (_insertedStages.Count > 0)
			{
				SetCurrentStage(_insertedStages.Dequeue());
				return;
			}

			if (_nextStageIndex >= _runtimeStages.Count) _nextStageIndex = 0;

			var stage = _runtimeStages.Count > 0 ? _runtimeStages[_nextStageIndex] : null;
			_nextStageIndex++;

			SetCurrentStage(stage);
		}

		public void GoTo(int index)
		{
			if (_runtimeStages.Count == 0) return;

			CurrentStage?.EndStage();
			_onStageEnded?.Invoke(CurrentStage);

			_nextStageIndex = Mathf.Clamp(index, 0, _runtimeStages.Count - 1);
			var stage = _runtimeStages[_nextStageIndex];
			_nextStageIndex++;

			SetCurrentStage(stage);
		}

		public void GoTo(PokerStage stage)
		{
			var index = _runtimeStages.IndexOf(Resolve(stage));
			if (index >= 0) GoTo(index);
		}

		// Queued to run as the next stage, after whatever is running now finishes on its own terms.
		public void Insert(PokerStage stage)
		{
			if (!stage) return;

			_insertedStages.Enqueue(Resolve(stage));
		}

		// Runs immediately on top, freezing everything below it until it is popped.
		public void PushOverlay(PokerStage stage)
		{
			if (!stage) return;

			var overlay = Resolve(stage);
			if (!overlay) return;

			ActiveStage?.PauseStage();

			_overlayStack.Add(overlay);
			_mode.Data.OverlayStageId.Value = overlay.StageId;

			overlay.StartStage();
			_onStageStarted?.Invoke(overlay);
		}

		public void PopOverlay()
		{
			if (_overlayStack.Count == 0) return;

			var overlay = _overlayStack[^1];
			_overlayStack.RemoveAt(_overlayStack.Count - 1);

			overlay.EndStage();
			_onStageEnded?.Invoke(overlay);

			_mode.Data.OverlayStageId.Value = CurrentOverlay ? CurrentOverlay.StageId : string.Empty;
			ActiveStage?.ResumeStage();
		}

		private void SetCurrentStage(PokerStage stage)
		{
			CurrentStage = stage;
			_mode.Data.StageId.Value = stage ? stage.StageId : string.Empty;

			// The clock belongs to whoever is running, so it never survives the handover.
			_mode.ClearStageTimer();

			if (!stage) return;

			stage.StartStage();
			_onStageStarted?.Invoke(stage);
		}

		private static PokerStage CreateRuntimeStage(PokerStage source)
		{
			var runtime = UnityEngine.Object.Instantiate(source);
			runtime.name = source.name;

			return runtime;
		}

		private void Release()
		{
			foreach (var stage in _runtimeStages)
			{
				if (stage) UnityEngine.Object.Destroy(stage);
			}

			foreach (var stage in _detachedStages)
			{
				if (stage) UnityEngine.Object.Destroy(stage);
			}

			_runtimeStages.Clear();
			_detachedStages.Clear();

			CurrentStage = null;
			_nextStageIndex = 0;
		}
	}
}
