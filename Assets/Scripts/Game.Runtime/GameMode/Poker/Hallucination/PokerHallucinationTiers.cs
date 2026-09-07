using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Hallucination
{
	// The ladder a rising hallucination climbs. Each rung is a percentage and a pool: crossing it picks
	// one effect out of that pool at random and leaves it running, so a player at the fourth rung is
	// wearing all four at once. That accumulation is the design — the world does not swap appearance at
	// each tier, it gets worse.
	//
	// There is no "tier" type of its own: a tier is only ever a threshold with a pool behind it, so the
	// list is the whole model and adding a fifth rung is a row in an asset.
	[CreateAssetMenu(fileName = "PokerHallucinationTiers", menuName = "Game/Poker/Hallucination Tiers")]
	public class PokerHallucinationTiers : ScriptableObject
	{
		[Serializable]
		public class Rung
		{
			[Tooltip("Hallucination at or above which this rung's effect starts. Crossing back below it takes the effect off again.")]
			[PropertyRange(1, 100)]
			[SerializeField] private int _threshold = 25;

			[Tooltip("Effects this rung can draw. One is picked at random each time the rung is climbed, so a player who sobers and climbs back sees something new.")]
			[SerializeField] private List<PokerHallucinationEffect> _pool = new();

			public int Threshold => Mathf.Clamp(_threshold, 1, 100);
			public IReadOnlyList<PokerHallucinationEffect> Pool => _pool;
		}

		[InfoBox("Order does not matter — each rung is found by its own threshold — but keeping them ascending is how anybody reads this asset.")]
		[SerializeField] private List<Rung> _rungs = new();

		public IReadOnlyList<Rung> Rungs => _rungs;
	}
}
