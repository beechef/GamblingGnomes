using System;
using Game.Runtime.GameMode.Poker.Player;
using Unity.Netcode;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Hallucination
{
	// A roll against somebody's life, played out where the table can watch it land. The effect decides the
	// number the moment the cap goes down and queues it here; the beat that is running then starts it with
	// its own pacing. Every screen sweeps the skull along that player's bar, stops it on the number and
	// holds it there long enough to be understood — and only once that hold is over does a fatal roll put
	// them under. A death landing in the same frame as the roll would be read off the pose before anybody
	// saw the dice.
	//
	// This holds no timing of its own. The beat hands it the durations, and the RPC carries them to every
	// client, so the skull, the death and the next mouthful are all paced by one asset.
	//
	// Three moments:
	// - OnRollStarted: the sweep begins after its lead-in;
	// - OnRollSettled: the skull has stopped — the seam for any feedback on the result;
	// - the hold ends: the consequence lands, and ServerRollRemaining reaches zero for the eating beat.
	public class PokerHallucinationRollController : NetworkBehaviour
	{
		[Header("References")]
		[SerializeField] private PokerPlayerData _data;
		[SerializeField] private PokerHallucinationController _hallucination;

		private bool _queued;
		private int _queuedRoll;
		private bool _queuedFatal;
		private int _queuedRateBefore;
		private int _queuedRateAfter;

		private float _rollEndsAt;

		// Raised on every client, host included: lead-in, sweep and hold in seconds, then the number it stops on.
		public event Action<float, float, float, int> OnRollStarted;

		// Raised on every client the moment the skull stops: the number, and whether it means going under.
		// Anything that reacts to the result — a sound, a flash, a word on screen — hangs off this.
		public event Action<int, bool> OnRollSettled;

		public bool ServerHasQueuedRoll => _queued;

		// How long the table still owes this roll, the hold on the result included.
		public float ServerRollRemaining => Mathf.Max(0f, _rollEndsAt - Time.time);

		public bool ServerRollFatal { get; private set; }

		private void Awake()
		{
			if (!_data) _data = GetComponentInParent<PokerPlayerData>();
			if (!_hallucination && _data) _hallucination = _data.GetComponentInChildren<PokerHallucinationController>(true);
		}

		// Roll is 0..99 and fatal when it lands under the rate, which is exactly where the skull stops inside
		// the filled part of the bar. Nothing happens until the running beat starts it.
		public void ServerQueueRoll(int roll, bool fatal, int rateBefore, int rateAfter)
		{
			if (!IsServer) return;

			_queued = true;
			_queuedRoll = roll;
			_queuedFatal = fatal;
			_queuedRateBefore = rateBefore;
			_queuedRateAfter = rateAfter;
		}

		public void ServerStartQueuedRoll(float leadIn, float sweep, float hold)
		{
			if (!IsServer || !_queued) return;

			_queued = false;

			// The sweep never plays behind closed eyes: a gain that crossed a rung is blinking right now.
			if (_hallucination && _hallucination.CrossesRung(_queuedRateBefore, _queuedRateAfter))
			{
				leadIn = Mathf.Max(leadIn, _hallucination.TransitionDuration);
			}

			var total = leadIn + sweep + hold;

			ServerRollFatal = _queuedFatal;
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
