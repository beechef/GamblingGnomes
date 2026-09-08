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

		// Rung index to the effect drawn for it. Absent means the rung is not climbed.
		private readonly Dictionary<int, PokerHallucinationEffect> _active = new();

		public int ActiveCount => _active.Count;

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

		private void Refresh()
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
			var effect = pool[Random.Range(0, pool.Count)];
			if (!effect) return;

			_active[index] = effect;
			effect.Begin(_player);
		}

		private void EndRung(int index)
		{
			if (!_active.TryGetValue(index, out var effect)) return;

			_active.Remove(index);

			if (effect) effect.End(_player);
		}

		private void EndAll()
		{
			foreach (var pair in _active)
			{
				if (pair.Value) pair.Value.End(_player);
			}

			_active.Clear();
		}
	}
}
