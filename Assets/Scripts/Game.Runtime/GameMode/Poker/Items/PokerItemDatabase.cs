using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Items
{
	// What a unit of stake can be. A unit replicates as an index into this asset — the same trade
	// PlayerColorDatabase makes — so the mushrooms can be renamed, recoloured and retuned without a
	// word of it reaching the network, and two clients can never disagree about what is in the pot.
	[CreateAssetMenu(fileName = "PokerItemDatabase", menuName = "Game/Poker/Item Database")]
	public class PokerItemDatabase : ScriptableObject
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

			[Tooltip("What this kind looks like on the table. Its own model per kind rather than one shape recoloured: a cap is a thing, and telling two of them apart by tint is a placeholder, not a design.")]
			[SerializeField] private GameObject _worldPrefab;

			[Tooltip("Rarity, against the other entries' weights. Zero never comes up without the entry losing its place in the list.")]
			[MinValue(0)]
			[SerializeField] private int _weight = 1;

			[Tooltip("What eating one does. Empty is a mushroom that only costs the swallowing.")]
			[SerializeField] private PokerItemEffect _effect;

			[Tooltip("Off, this kind can never be wagered — it is only ever fed to somebody on purpose. The Colorful cap is the case.")]
			[SerializeField] private bool _wagerable = true;

			public string DisplayName => _displayName;
			public Sprite Icon => _icon;
			public Color Color => _color;
			public GameObject WorldPrefab => _worldPrefab;
			public int Weight => Mathf.Max(0, _weight);
			public PokerItemEffect Effect => _effect;
			public bool Wagerable => _wagerable;
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
				// A kind nobody may wager is not one the table deals at random either.
				if (entry != null && entry.Wagerable) totalWeight += entry.Weight;
			}

			if (totalWeight <= 0) return PlainChip;

			var roll = UnityEngine.Random.Range(0, totalWeight);

			for (var i = 0; i < _entries.Count && i < byte.MaxValue; i++)
			{
				if (_entries[i] == null) continue;

				if (!_entries[i].Wagerable) continue;

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
