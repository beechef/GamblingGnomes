using System;
using System.Threading;
using System.Collections.Generic;
using Game.Runtime.GameMode.Poker.Player;
using Unity.Netcode;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Hallucination
{
	// Runs the rungs this player has climbed. Owner-only, because what a hallucination looks like is
	// drawn on one screen and is the one thing at this table that is deliberately not shared.
	//
	// A rung's effect is picked when the rung is *climbed*, not when it is read: sobering back down takes
	// it off, and climbing again draws a fresh one. That is what makes an item that lowers the rate
	// visible — the world clears, and coming back is a different room rather than the same one returning.
	public class PokerHallucinationController : NetworkBehaviour
	{
		[Header("References")]
		[SerializeField] private PokerPlayer _player;
		[SerializeField] private PokerPlayerData _data;

		[Tooltip("Which rungs exist and what each can draw. Empty plays the round with no hallucinations at all, which is what a table testing the card rules wants.")]
		[SerializeField] private PokerHallucinationTiers _tiers;

		[Tooltip("Seconds the screen takes to blink when a rung is climbed or lost. Zero applies the change outright, which is what a table testing the ladder wants.")]
		[SerializeField] private float _transitionDuration = 0.5f;

		[Tooltip("Where the running effects are hung. Empty hangs them on this object, which is what a player prefab wants.")]
		[SerializeField] private Transform _effectRoot;

		// Rung index to the object running it. Absent means the rung is not climbed.
		private readonly Dictionary<int, PokerHallucinationEffectBehaviour> _active = new();

		private bool _transitioning;

		public int ActiveCount => _active.Count;

		// The whole blink, start to finish. The rungs land halfway through it, and the view that draws
		// the eyelids reads this same number rather than carrying one of its own to keep in step.
		public float TransitionDuration => Mathf.Max(0f, _transitionDuration);

		public event Action OnTransitionStarted;

		public override void OnNetworkSpawn()
		{
			if (!IsOwner) return;

			if (!_player) _player = GetComponentInParent<PokerPlayer>();
			if (!_data) _data = GetComponentInParent<PokerPlayerData>();
			if (!_data) return;

			_data.OnHallucinationChanged += HandleChanged;

			// Read rather than waited for: a client arriving at a table already halfway under has no
			// change coming to tell it so.
			Refresh();
		}

		public override void OnNetworkDespawn()
		{
			if (_data) _data.OnHallucinationChanged -= HandleChanged;

			// Everything comes off on the way out: an effect is a change to this client's whole view, and
			// leaving one running would outlive the table it belonged to.
			EndAll();
		}

		private void HandleChanged(int previous, int current) => Refresh();

		// The blink is there to cover the swap, so the rungs are not allowed to land while the eye is open.
		// A change arriving mid-blink is folded into the one already running rather than queued behind it:
		// ApplyRungs reads the rate at the moment it runs, so the later change is what lands anyway, and a
		// queue would spend a second blink saying nothing new.
		private void Refresh()
		{
			if (!_tiers || !_data) return;
			if (!AnyRungWouldChange()) return;

			if (TransitionDuration <= 0f)
			{
				ApplyRungs();
				return;
			}

			if (_transitioning) return;

			_ = RunTransition(destroyCancellationToken);
		}

		private async Awaitable RunTransition(CancellationToken ct)
		{
			_transitioning = true;

			try
			{
				OnTransitionStarted?.Invoke();

				// Unscaled: a blink is exactly the sort of beat a paused game would otherwise hold open
				// forever.
				var deadline = Time.unscaledTime + TransitionDuration * 0.5f;
				while (Time.unscaledTime < deadline) await Awaitable.NextFrameAsync(ct);

				ApplyRungs();
			}
			finally
			{
				_transitioning = false;
			}

			// Whatever arrived while the eye was shut has already landed, but a rate that moved again after
			// ApplyRungs deserves its own beat rather than being lost to the guard above.
			Refresh();
		}

		private bool AnyRungWouldChange()
		{
			var rate = _data.HallucinationRate.Value;
			var rungs = _tiers.Rungs;

			for (var i = 0; i < rungs.Count; i++)
			{
				if (rungs[i] == null) continue;

				if (rate >= rungs[i].Threshold != _active.ContainsKey(i)) return true;
			}

			return false;
		}

		private void ApplyRungs()
		{
			if (!_tiers || !_data) return;

			var rate = _data.HallucinationRate.Value;
			var rungs = _tiers.Rungs;

			for (var i = 0; i < rungs.Count; i++)
			{
				var rung = rungs[i];
				if (rung == null) continue;

				var climbed = rate >= rung.Threshold;
				var running = _active.ContainsKey(i);

				if (climbed == running) continue;

				if (climbed) BeginRung(i, rung);
				else EndRung(i);
			}
		}

		private void BeginRung(int index, PokerHallucinationTiers.Rung rung)
		{
			var pool = rung.Pool;
			if (pool == null || pool.Count == 0) return;

			// Drawn here rather than held on the rung, so two players on the same rung are not looking at
			// the same thing and one player climbing it twice is not either.
			var asset = pool[UnityEngine.Random.Range(0, pool.Count)];
			if (!asset) return;

			// The asset is config and the object is the effect. The same effect is allowed to sit in two
			// pools on purpose, so anything it mutates has to live per rung — an asset holding what it
			// spawned would let the lower rung's End tear down what the higher one believes it owns, and one
			// of the two would silently do nothing. An object per rung makes stacking work by construction,
			// and it names what this player is seeing in the hierarchy, where it can be watched and retuned
			// while it is on screen.
			var behaviour = asset.Run(_effectRoot ? _effectRoot : transform, _player);
			if (!behaviour) return;

			behaviour.name = $"Rung {index} ({rung.Threshold}%) - {asset.name}";
			_active[index] = behaviour;
		}

		private void EndRung(int index)
		{
			if (!_active.TryGetValue(index, out var behaviour)) return;

			_active.Remove(index);

			// Stop is what takes the object down, on its own terms: an effect that eases out keeps its host
			// alive for exactly as long as the ease takes.
			if (behaviour) behaviour.Stop();
		}

		private void EndAll()
		{
			foreach (var pair in _active)
			{
				if (pair.Value) pair.Value.Stop();
			}

			_active.Clear();
		}
	}
}
