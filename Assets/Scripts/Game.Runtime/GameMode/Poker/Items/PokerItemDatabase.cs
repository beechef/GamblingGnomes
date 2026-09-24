using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Items
{
	// The items one mode deals, and how often each comes up. An item asset can sit in several databases;
	// the weight belongs to the row, because how common a card is is a question about the mode.
	[CreateAssetMenu(fileName = "PokerItemDatabase", menuName = "Game/Poker/Item Database")]
	public class PokerItemDatabase : ScriptableObject
	{
		[Serializable]
		public class Entry
		{
			[Required]
			[SerializeField] private PokerItem _item;

			[Tooltip("Relative chance of being dealt. Zero never deals the item, though it can still be held.")]
			[MinValue(0)]
			[SerializeField] private int _weight = 1;

			public PokerItem Item => _item;
			public int Weight => Mathf.Max(0, _weight);
		}

		[InfoBox("Two rows name the same item type; only the first is ever found.", InfoMessageType.Error, nameof(HasDuplicateTypes))]
		[SerializeField] private List<Entry> _entries = new();

		public IReadOnlyList<Entry> Entries => _entries;

		private bool HasDuplicateTypes
		{
			get
			{
				var seen = new HashSet<PokerItemType>();
				foreach (var entry in _entries)
				{
					if (entry != null && entry.Item && !seen.Add(entry.Item.Type)) return true;
				}

				return false;
			}
		}

		public bool TryGetItem(PokerItemType type, out PokerItem item)
		{
			foreach (var entry in _entries)
			{
				if (entry == null || !entry.Item || entry.Item.Type != type) continue;

				item = entry.Item;
				return true;
			}

			item = null;
			return false;
		}

		// Weighted, and never the same item twice in one handout: the draw is removed from the pool once taken.
		public void Draw(int count, List<PokerItemType> results)
		{
			results.Clear();

			var pool = new List<Entry>();
			foreach (var entry in _entries)
			{
				if (entry != null && entry.Item && entry.Weight > 0) pool.Add(entry);
			}

			while (results.Count < count && pool.Count > 0)
			{
				var total = 0;
				foreach (var entry in pool) total += entry.Weight;

				var roll = UnityEngine.Random.Range(0, total);
				for (var i = 0; i < pool.Count; i++)
				{
					roll -= pool[i].Weight;
					if (roll >= 0) continue;

					results.Add(pool[i].Item.Type);
					pool.RemoveAt(i);
					break;
				}
			}
		}
	}
}
