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

		// The number the helper gives a hand: a house hand is 0, the standard ranking counts from 1, best first,
		// so the board and anything naming a hand's place read the same number. -1 for a hand not in the table.
		public int DisplayRankOf(PokerHandType hand)
		{
			if (!hand) return -1;
			if (hand.IsHouseHand) return 0;

			var rank = 0;

			foreach (var handType in HandTypes)
			{
				if (handType.IsHouseHand) continue;

				rank++;
				if (handType == hand) return rank;
			}

			return -1;
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
