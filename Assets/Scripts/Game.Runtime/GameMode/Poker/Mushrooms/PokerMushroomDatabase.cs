using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Mushrooms
{
	// What a unit of stake can be. A unit replicates as an index into this asset — the same trade
	// PlayerColorDatabase makes — so the mushrooms can be renamed, recoloured and retuned without a
	// word of it reaching the network, and two clients can never disagree about what is in the pot.
	[CreateAssetMenu(fileName = "PokerMushroomDatabase", menuName = "Game/Poker/Mushroom Database")]
	public class PokerMushroomDatabase : ScriptableObject
	{
		// Index zero stays the plain chip — a unit with no identity — so a ledger written before this
		// asset existed still means what it always meant, and a table with no database wired plays
		// exactly the money game it played yesterday.
		public const byte PlainChip = 0;

		[Serializable]
		public class Entry
		{
			[SerializeField] private string _displayName;
			[SerializeField] private Sprite _icon;
			[SerializeField] private Color _color = Color.white;

			[Tooltip("Rarity, against the other entries' weights. Zero never comes up without the entry losing its place in the list.")]
			[MinValue(0)]
			[SerializeField] private int _weight = 1;

			[Tooltip("What eating one does. Empty is a mushroom that only costs the swallowing.")]
			[SerializeField] private PokerMushroomEffect _effect;

			public string DisplayName => _displayName;
			public Sprite Icon => _icon;
			public Color Color => _color;
			public int Weight => Mathf.Max(0, _weight);
			public PokerMushroomEffect Effect => _effect;
		}

		[InfoBox("Order carries meaning: a unit replicates as its position here, one-based. Reordering or removing an entry silently re-types every stake already dealt — append instead.")]
		[SerializeField] private List<Entry> _entries = new();

		public IReadOnlyList<Entry> Entries => _entries;

		// Weighted, so rarity is a number on the entry rather than duplicate entries in the list.
		// An empty or all-zero database draws plain chips, which is the money game.
		public byte DrawItemType()
		{
			var totalWeight = 0;
			foreach (var entry in _entries)
			{
				if (entry != null) totalWeight += entry.Weight;
			}

			if (totalWeight <= 0) return PlainChip;

			var roll = UnityEngine.Random.Range(0, totalWeight);

			for (var i = 0; i < _entries.Count && i < byte.MaxValue; i++)
			{
				if (_entries[i] == null) continue;

				roll -= _entries[i].Weight;
				if (roll < 0) return (byte)(i + 1);
			}

			return PlainChip;
		}

		public bool TryGetEntry(byte itemType, out Entry entry)
		{
			entry = null;

			if (itemType == PlainChip) return false;

			var index = itemType - 1;
			if (index >= _entries.Count) return false;

			entry = _entries[index];
			return entry != null;
		}
	}
}
