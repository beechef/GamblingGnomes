using System;
using Game.Runtime.GameMode.Poker.Player;
using Sirenix.OdinInspector;
using Unity.Netcode;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Hallucination
{
	// A roll against somebody's life, played out where the table can watch it land, and the one place a roll
	// is made. Whatever sets one off (a Colorful cap, an item) only says what it is rolled against; the number
	// is drawn here the moment it is queued, and the beat that is running decides when it starts.
	// Every screen sweeps the skull along that player's bar, stops it on the number and holds it there long
	// enough to be understood — and only once that hold is over does a fatal roll put them under. A death
	// landing in the same frame as the roll would be read off the pose before anybody saw the dice.
	//
	// Every roll plays on one PokerRollPacing, which the RPC carries to every client, so the skull, the death
	// and whatever waits on them are paced by one asset whoever set the roll off.
	//
	// Three moments:
	// - OnRollStarted: the sweep begins after its lead-in;
	// - OnRollSettled: the skull has stopped — the seam for any feedback on the result;
	// - the hold ends: the consequence lands, and ServerRollRemaining reaches zero for the eating beat.
	public class PokerHallucinationRollController : NetworkBehaviour
	{
		[Header("Pacing")]
		[Required]
		[SerializeField] private PokerRollPacing _pacing;

		[Header("References")]
		[SerializeField] private PokerPlayerData _data;
		[SerializeField] private PokerHallucinationController _hallucination;
		[SerializeField] private PokerDeathPoseController _deathPose;

		private bool _queued;
		private int _queuedRoll;
		private bool _queuedFatal;
		private int _queuedRateBefore;
		private int _queuedRateAfter;

		private float _rollEndsAt;
		private int _rolledRate;

		// Raised on every client, host included: lead-in, sweep and hold in seconds, then the number it stops on.
		public event Action<float, float, float, int> OnRollStarted;

		// Raised on every client the moment the skull stops: the number, and whether it means going under.
		// Anything that reacts to the result — a sound, a flash, a word on screen — hangs off this.
		public event Action<int, bool> OnRollSettled;

		public bool ServerHasQueuedRoll => _queued;

		// How long the table still owes this roll, the hold on the result included.
		public float ServerRollRemaining => Mathf.Max(0f, _rollEndsAt - Time.time);

		public bool ServerRollFatal { get; private set; }

		// How long the table still owes this roll and what it set off: the sweep and the hold, then the whole
		// death when it was fatal, so whatever follows never lands on top of the fall.
		public float ServerOutcomeRemaining
		{
			get
			{
				var remaining = ServerRollRemaining;
				if (remaining <= 0f) return 0f;
				if (!ServerRollFatal || !_deathPose) return remaining;

				return remaining + _deathPose.DeathWait(_rolledRate, PokerPlayerData.MaxHallucination);
			}
		}

		private void Awake()
		{
			if (!_data) _data = GetComponentInParent<PokerPlayerData>();
			if (!_hallucination && _data) _hallucination = _data.GetComponentInChildren<PokerHallucinationController>(true);
			if (!_deathPose && _data) _deathPose = _data.GetComponentInChildren<PokerDeathPoseController>(true);
		}

		// Rolls 0..99 against `against`, fatal when it lands under it, which is exactly where the skull stops
		// inside the filled part of the bar. rateBefore is the rate before whatever set the roll off, so a rung
		// crossed on the way is blinked before the sweep. Nothing happens until the running beat starts it.
		// False when there is nothing to roll: no rate to roll against, or already at the ceiling, where rolling
		// would only invite a reading in which the highest rate somehow survives.
		public bool ServerQueueRoll(int against, int rateBefore)
		{
			if (!IsServer || !_data || !_data.IsAlive || against <= 0) return false;

			var roll = UnityEngine.Random.Range(0, PokerPlayerData.MaxHallucination);

			_queued = true;
			_queuedRoll = roll;
			_queuedFatal = roll < against;
			_queuedRateBefore = rateBefore;
			_queuedRateAfter = _data.HallucinationRate.Value;

			return true;
		}

		// Rolled and shown at once, for something that is its own beat rather than a mouthful the eating paces.
		public bool ServerRoll(int against, int rateBefore)
		{
			if (!ServerQueueRoll(against, rateBefore)) return false;

			ServerStartQueuedRoll();
			return true;
		}

		public void ServerStartQueuedRoll()
		{
			if (!IsServer || !_queued) return;

			_queued = false;

			var leadIn = _pacing ? _pacing.LeadIn : 0f;
			var sweep = _pacing ? _pacing.SweepDuration : 0.1f;
			var hold = _pacing ? _pacing.ResultHold : 0f;

			// The sweep never plays behind closed eyes: a gain that crossed a rung is blinking right now.
			if (_hallucination && _hallucination.CrossesRung(_queuedRateBefore, _queuedRateAfter))
			{
				leadIn = Mathf.Max(leadIn, _hallucination.TransitionDuration);
			}

			var total = leadIn + sweep + hold;

			ServerRollFatal = _queuedFatal;
			_rolledRate = _queuedRateAfter;
			_rollEndsAt = Time.time + total;

			RollRPC(leadIn, sweep, hold, _queuedRoll, _queuedFatal);

			if (_queuedFatal) _ = ApplyFatalAfter(total);
		}

		// A roll nobody started still has to be paid — a beat ending under it must not quietly spare somebody.
		public void ServerFlushQueuedRoll()
		{
			if (!IsServer || !_queued) return;

			_queued = false;

			if (_queuedFatal) ApplyFatal();
		}

		[Rpc(SendTo.Everyone)]
		private void RollRPC(float leadIn, float sweep, float hold, int roll, bool fatal)
		{
			OnRollStarted?.Invoke(leadIn, sweep, hold, roll);

			_ = RaiseSettledAfter(leadIn + sweep, roll, fatal);
		}

		private async Awaitable RaiseSettledAfter(float delay, int roll, bool fatal)
		{
			if (!await WaitAsync(delay)) return;

			OnRollSettled?.Invoke(roll, fatal);
		}

		private async Awaitable ApplyFatalAfter(float delay)
		{
			if (!await WaitAsync(delay)) return;

			ApplyFatal();
		}

		// Going under is still written as the ceiling, so IsAlive stays the one question anything asks.
		private void ApplyFatal()
		{
			if (_data && _data.IsAlive) _data.ServerChangeHallucination(PokerPlayerData.MaxHallucination);
		}

		private async Awaitable<bool> WaitAsync(float delay)
		{
			try
			{
				await Awaitable.WaitForSecondsAsync(delay, destroyCancellationToken);
				return true;
			}
			catch (OperationCanceledException)
			{
				return false;
			}
		}
	}
}
