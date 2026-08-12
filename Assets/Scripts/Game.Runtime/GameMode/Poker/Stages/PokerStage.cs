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

		public string StageId => string.IsNullOrEmpty(_stageId) ? name : _stageId;

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

		public void StartStage()
		{
			IsRunning = true;
			IsPaused = false;
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
			OnPauseStage();
		}

		public void ResumeStage()
		{
			if (!IsRunning || !IsPaused) return;

			IsPaused = false;
			OnResumeStage();
		}

		public void TickStage(float deltaTime)
		{
			if (!IsRunning || IsPaused) return;

			OnTickStage(deltaTime);
		}

		public virtual bool HandleAction(ulong clientId, PokerActionType action, int amount) => false;

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
	}
}
