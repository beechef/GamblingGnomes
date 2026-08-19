using Game.Runtime.GameMode.Poker.Stages;
using Sirenix.OdinInspector;
using Unity.Netcode;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Modules
{
	// The lights go out once a hand, at a moment nobody is given. Everything a player was reading — the
	// board, the seat they were watching, the clock — is gone for a few seconds and the hand carries on
	// underneath it, which is the whole event: not a rule change, a few seconds of everyone being equally
	// blind.
	//
	// It decides nothing and forbids nothing, so it is a replicated bool and not an overlay stage: the turn
	// keeps running, the timer keeps counting, and every client draws the dark for itself.
	public class PokerBlackoutModule : PokerModule
	{
		[Header("Toggle")]
		[Tooltip("Where the switch starts. Flip the replicated value at runtime to turn the event on or off for the table.")]
		[SerializeField] private bool _enabledByDefault = true;

		[Header("Timing")]
		[Tooltip("Times the lights may go in one hand. Zero turns the event off for good; more than one makes it weather rather than an event.")]
		[MinValue(0)]
		[SerializeField] private int _blackoutsPerHand = 1;

		[Tooltip("Seconds after the hand is dealt before the lights can go, drawn fresh each time. A range rather than a number so nobody can learn to brace for it.")]
		[MinMaxSlider(1f, 180f, true)]
		[SerializeField] private Vector2 _delayRange = new(15f, 60f);

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

		// Armed off the deal rather than off the game starting: a match runs itself out over many hands now,
		// and "once a round" is once a hand.
		public override void OnStageStarted(PokerStage stage)
		{
			if (!IsServer || !(stage is PokerDealStage)) return;

			_remainingThisHand = Mathf.Max(0, _blackoutsPerHand);

			Arm();
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

			_armed = false;
			_remainingThisHand--;

			_lightsBackTime = now + Random.Range(Mathf.Min(_durationRange.x, _durationRange.y),
				Mathf.Max(_durationRange.x, _durationRange.y));

			IsDark.Value = true;
		}

		private void Arm()
		{
			if (_remainingThisHand <= 0)
			{
				_armed = false;
				return;
			}

			_nextBlackoutTime = NetworkManager.ServerTime.Time
				+ Random.Range(Mathf.Min(_delayRange.x, _delayRange.y), Mathf.Max(_delayRange.x, _delayRange.y));

			_armed = true;
		}

		private void EndBlackoutServer()
		{
			if (IsDark.Value) IsDark.Value = false;

			// However many are left in this hand, the next one is drawn from here rather than from the
			// moment the hand was dealt — two blackouts back to back would read as a fault, not an event.
			Arm();
		}
	}
}
