using Unity.Netcode;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Stages
{
	// Stages are NetworkBehaviours so a stage can carry its own replicated state and RPCs — an
	// overlay that runs a vote needs both, and nothing about the machine should force it back into
	// the shared table data.
	public abstract class PokerStage : NetworkBehaviour
	{
		[Header("Stage")]
		[Tooltip("Replicated to clients so UI can key off the running stage without knowing the type.")]
		[SerializeField] private string _stageId;

		public string StageId => string.IsNullOrEmpty(_stageId) ? GetType().Name : _stageId;

		public PokerGameMode GameMode { get; private set; }
		public bool IsRunning { get; private set; }
		public bool IsPaused { get; private set; }

		protected PokerGameData Data => GameMode ? GameMode.Data : null;
		protected PokerRuleSettings Rules => GameMode ? GameMode.Rules : null;

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
