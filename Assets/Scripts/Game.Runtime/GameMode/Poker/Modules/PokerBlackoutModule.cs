using System;
using System.Collections.Generic;
using Game.Runtime.GameMode.Config;
using Game.Runtime.GameMode.Poker.Stages;
using Sirenix.OdinInspector;
using Unity.Netcode;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Modules
{
	// The lights go out at a moment nobody is given. Everything a player was reading — the board, the seat
	// they were watching, the clock — is gone for a few seconds and the hand carries on underneath it,
	// which is the whole event: not a rule change, a few seconds of everyone being equally blind.
	//
	// It decides nothing and forbids nothing, so it is a replicated bool and not an overlay stage: the turn
	// keeps running, the timer keeps counting, and every client draws the dark for itself.
	public class PokerBlackoutModule : PokerModule
	{
		// A stage and how likely it is to go dark. Two numbers rather than one, because when it might
		// happen and whether it does are different questions — the moment is picked as the stage opens and
		// the chance is not rolled until that moment arrives, so a street that ends early simply never had
		// its roll.
		[Serializable]
		private struct StageChance
		{
			[Tooltip("The stage this chance belongs to. Picked rather than named, so it cannot drift off a renamed asset.")]
			public PokerStage Stage;

			[Range(0f, 1f)]
			[Tooltip("Odds the lights go during it, rolled once at the moment picked inside it.")]
			public float Chance;
		}

		[Header("Toggle")]
		[Tooltip("Where the switch starts. Flip the replicated value at runtime to turn the event on or off for the table.")]
		[SerializeField] private bool _enabledByDefault = true;

		[Header("Odds")]
		[Tooltip("Which stages are dangerous, and how dangerous. The list is the whole schedule: a street can be made likely to go dark and the deal left alone.")]
		[SerializeField] private List<StageChance> _stageChances = new();

		[Tooltip("Odds for a stage the list does not name. Zero keeps the event to exactly the stages that were picked.")]
		[Range(0f, 1f)]
		[SerializeField] private float _defaultChance;

		[Tooltip("Most times the lights may actually go in one hand, however many stages roll for it. Zero turns the event off for good.")]
		[MinValue(0)]
		[SerializeField] private int _blackoutsPerHand = 1;

		[Header("Timing")]
		[Tooltip("Seconds into a stage that its moment falls, drawn fresh each time. A range rather than a number so nobody can learn to brace for it — and a stage that ends before its moment arrives simply never rolled.")]
		[MinMaxSlider(0f, 120f, true)]
		[SerializeField] private Vector2 _delayRange = new(4f, 25f);

		[Tooltip("How long the dark lasts, drawn fresh each time.")]
		[MinMaxSlider(0.5f, 20f, true)]
		[SerializeField] private Vector2 _durationRange = new(2f, 6f);

		// Read by every client's screen. The server owns when and for how long; what the dark looks like is
		// the view's business entirely.
		[HideInInspector] public NetworkVariable<bool> IsDark = new(false,
			readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);

		[HideInInspector] public NetworkVariable<bool> Enabled = new(true,
			readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);

		private int _remainingThisHand;
		private double _nextBlackoutTime;
		private double _lightsBackTime;
		private float _armedChance;
		private bool _armed;

		public override void OnNetworkSpawn()
		{
			base.OnNetworkSpawn();

			if (IsServer) Enabled.Value = _enabledByDefault;
		}

		public void SetEnabledServer(bool enabled)
		{
			if (!IsServer) return;

			Enabled.Value = enabled;

			if (!enabled) EndBlackoutServer();
		}

		// Chance zero is the off switch — ArmFor already refuses to arm on it, so no separate toggle.
		protected override void OnCollectConfigEntries(List<MatchConfigEntry> entries)
		{
			entries.Add(new MatchConfigFloat(ModuleId, "Blackout", "Chance", "Chance", 0f, 1f, 0.05f,
				() => _defaultChance, value => _defaultChance = value, "0%"));
			entries.Add(new MatchConfigFloat(ModuleId, "Blackout", "DurationMin", "Duration Min", 0.5f, 20f, 0.5f,
				() => _durationRange.x,
				value => _durationRange = new Vector2(value, Mathf.Max(value, _durationRange.y)), "0.#"));
			entries.Add(new MatchConfigFloat(ModuleId, "Blackout", "DurationMax", "Duration Max", 0.5f, 20f, 0.5f,
				() => _durationRange.y,
				value => _durationRange = new Vector2(Mathf.Min(_durationRange.x, value), value), "0.#"));
		}

		// Every stage gets its own shot. The per-hand budget is reset on the deal rather than on the game
		// starting: a match runs itself out over many hands now, and "once a round" is once a hand.
		public override void OnStageStarted(PokerStage stage)
		{
			if (!IsServer) return;

			if (stage is PokerDealStage) _remainingThisHand = Mathf.Max(0, _blackoutsPerHand);

			ArmFor(stage);
		}

		// The window belonged to that stage. A moment that never came around does not carry into the next
		// one, or a short street would keep handing its roll to whatever followed it.
		public override void OnStageEnded(PokerStage stage)
		{
			if (!IsServer) return;

			_armed = false;
		}

		// The dark must not outlive the hand it belonged to: a ranking board read by torchlight is nobody's
		// idea of an event.
		public override void OnGameEnded()
		{
			if (!IsServer) return;

			_remainingThisHand = 0;
			_armed = false;

			EndBlackoutServer();
		}

		// A NetworkBehaviour update, not a poll for a dependency: the event happens on a clock and somebody
		// has to notice the moment it does.
		private void Update()
		{
			if (!IsServer || !IsSpawned) return;

			var now = NetworkManager.ServerTime.Time;

			if (IsDark.Value)
			{
				if (now >= _lightsBackTime) EndBlackoutServer();
				return;
			}

			if (!_armed || !Enabled.Value || now < _nextBlackoutTime) return;

			// Spent whether or not it lands. One moment per stage is the point — a stage that rolled and
			// missed stays lit, rather than trying again until it succeeds.
			_armed = false;

			if (_remainingThisHand <= 0) return;
			if (UnityEngine.Random.value > _armedChance) return;

			_remainingThisHand--;

			_lightsBackTime = now + RandomIn(_durationRange);

			IsDark.Value = true;
		}

		// The moment first, the odds second: the chance is carried until it arrives rather than settled
		// here, so what is picked now is only *when* the table would find out.
		private void ArmFor(PokerStage stage)
		{
			_armedChance = ChanceFor(stage);

			if (_remainingThisHand <= 0 || _armedChance <= 0f)
			{
				_armed = false;
				return;
			}

			_nextBlackoutTime = NetworkManager.ServerTime.Time + RandomIn(_delayRange);
			_armed = true;
		}

		private float ChanceFor(PokerStage stage)
		{
			if (!stage) return 0f;

			foreach (var entry in _stageChances)
			{
				if (entry.Stage == stage) return Mathf.Clamp01(entry.Chance);
			}

			return Mathf.Clamp01(_defaultChance);
		}

		private void EndBlackoutServer()
		{
			if (IsDark.Value) IsDark.Value = false;

			// Whatever is still running gets a fresh moment, drawn from here rather than from when the stage
			// opened — two blackouts back to back would read as a fault, not an event.
			if (_remainingThisHand > 0) ArmFor(GameMode ? GameMode.CurrentStage : null);
		}

		private static float RandomIn(Vector2 range)
		{
			return UnityEngine.Random.Range(Mathf.Min(range.x, range.y), Mathf.Max(range.x, range.y));
		}
	}
}
