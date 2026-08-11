using System.Collections.Generic;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Hands
{
	[CreateAssetMenu(fileName = "PokerHandDatabase", menuName = "Game/Poker Hand Database")]
	public class PokerHandDatabase : ScriptableObject
	{
		[Tooltip("Every hand the table recognises. Order does not matter — they are ranked by tier.")]
		[SerializeField] private List<PokerHandType> _handTypes = new();

		private readonly List<PokerHandType> _sorted = new();

		public IReadOnlyList<PokerHandType> HandTypes
		{
			get
			{
				if (_sorted.Count != _handTypes.Count) RebuildSorted();
				return _sorted;
			}
		}

		private void OnEnable() => RebuildSorted();

		private void RebuildSorted()
		{
			_sorted.Clear();

			foreach (var handType in _handTypes)
			{
				if (handType) _sorted.Add(handType);
			}

			_sorted.Sort((left, right) => right.Tier.CompareTo(left.Tier));
		}
	}
}
