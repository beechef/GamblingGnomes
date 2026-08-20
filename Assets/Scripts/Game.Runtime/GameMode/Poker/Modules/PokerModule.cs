using System.Collections.Generic;
using Game.Runtime.GameMode.Config;
using Game.Runtime.GameMode.Poker.Stages;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Modules
{
	// A module is the unit that gets plugged into the mode to change what the game offers — extra
	// stages, extra player commands, extra restrictions. What it adds is up to it: a cheating module
	// handing players new abilities is one shape, a house-rules module vetoing actions is another.
	//
	// NetworkBehaviour rather than a plain component so a module owns its replicated state and RPCs
	// instead of having to widen the shared table data every time a new one is written.
	public abstract class PokerModule : NetworkBehaviour
	{
		[Header("Module")]
		[SerializeField] private string _moduleId;

		public string ModuleId => string.IsNullOrEmpty(_moduleId) ? GetType().Name : _moduleId;

		public PokerGameMode GameMode { get; private set; }

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

		// Stages contributed here join the round in sequence order; a module wanting to interrupt
		// instead should hold its stage back and call InsertStage or PushOverlay on the mode.
		public virtual void CollectStages(List<PokerStage> stages) { }

		// The stages held back for that: they take no slot in the loop, but every peer still needs its own
		// clone of them. Pushing one only ever happens on the server, so a client is never the one to make
		// the clone — and a UI asking about the overlay it can see running would find nothing to ask.
		public virtual void CollectReferencedStages(List<PokerStage> stages) { }

		// What this module offers the host to tune, declared under its ModuleId. A new module that
		// declares here appears in the match-config panel with no UI work of its own.
		public void CollectConfigEntries(List<MatchConfigEntry> entries) => OnCollectConfigEntries(entries);

		protected virtual void OnCollectConfigEntries(List<MatchConfigEntry> entries) { }

		public virtual void OnGameStarted() { }
		public virtual void OnGameEnded() { }
		public virtual void OnStageStarted(PokerStage stage) { }
		public virtual void OnStageEnded(PokerStage stage) { }
		public virtual void OnPlayerSeated(ulong clientId, int seatIndex) { }
		public virtual void OnPlayerLeftSeat(ulong clientId) { }
		// The moment the table actually asks somebody for something, which is where a house rule about what
		// they are able to answer with belongs. Raised for an overlay's turns too — whether one of those
		// counts is the module's business, and CurrentOverlay is how it tells them apart.
		public virtual void OnTurnBegan(ulong clientId) { }

		public virtual void OnPlayerActed(ulong clientId, PokerActionType action, int amount) { }

		public virtual bool CanStartGame() => true;
		public virtual bool CanLeaveSeat(ulong clientId) => true;
		public virtual bool CanPlayerAct(ulong clientId, PokerActionType action, int amount) => true;

		// Player facing entry point for whatever the module exposes — an ability activation, a vote,
		// a cheat attempt. Returning true marks the command as consumed.
		public virtual bool HandleCommandServer(ulong clientId, FixedString32Bytes commandId, int payload) => false;

		protected virtual void OnInitialize() { }
		protected virtual void OnDeInitialize() { }
	}
}
