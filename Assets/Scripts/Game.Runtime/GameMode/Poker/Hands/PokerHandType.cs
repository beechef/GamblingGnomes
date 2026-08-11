using System.Collections.Generic;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Hands
{
	// One asset per hand. Tier is the whole ranking: a house rule that beats a straight flush is a new
	// asset with a higher tier, and nothing else in the game has to know it exists.
	public abstract class PokerHandType : ScriptableObject
	{
		[Header("Hand")]
		[SerializeField] private string _displayName;

		[Tooltip("Higher beats lower. Standard hands run 0 (high card) to 8 (straight flush).")]
		[SerializeField] private int _tier;

		public string DisplayName => string.IsNullOrEmpty(_displayName) ? name : _displayName;
		public int Tier => _tier;

		// Kickers are appended highest first and decide ties between two hands of the same tier.
		public abstract bool TryEvaluate(PokerCardAnalysis analysis, List<int> kickers);
	}
}
