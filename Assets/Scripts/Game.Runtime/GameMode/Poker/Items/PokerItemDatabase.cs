using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Items
{
	// What a unit of stake can be. A unit replicates as its PokerItemType — the same trade
	// PlayerColorDatabase makes with a colour index — so the mushrooms can be renamed, recoloured and
	// retuned without a word of it reaching the network, and two clients can never disagree about what is
	// in the pot.
	[CreateAssetMenu(fileName = "PokerItemDatabase", menuName = "Game/Poker/Item Database")]
	public class PokerItemDatabase : ScriptableObject
	{
		public const PokerItemType PlainChip = PokerItemType.PlainChip;

		[Serializable]
		public class Entry
		{
			[Tooltip("Which kind this row describes. Named on the row rather than taken from its position, so reordering the list is a tidy-up rather than a silent re-typing of every stake already dealt.")]
			[SerializeField] private PokerItemType _type = PokerItemType.Green;

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

			public PokerItemType Type => _type;
			public string DisplayName => _displayName;
			public Sprite Icon => _icon;
			public Color Color => _color;
			public GameObject WorldPrefab => _worldPrefab;
			public int Weight => Mathf.Max(0, _weight);
			public PokerItemEffect Effect => _effect;
			public bool Wagerable => _wagerable;
		}

		[InfoBox("Each row names the kind it describes, so the order here is only a reading order. What must never change is a kind's value in PokerItemType — that is what replicates.")]
		[SerializeField] private List<Entry> _entries = new();

		public IReadOnlyList<Entry> Entries => _entries;

		// Weighted, so rarity is a number on the entry rather than duplicate entries in the list.
		// An empty or all-zero database draws plain chips, which is the money game.
		public PokerItemType DrawItemType()
		{
			var totalWeight = 0;
			foreach (var entry in _entries)
			{
				// A kind nobody may wager is not one the table deals at random either.
				if (entry != null && entry.Wagerable) totalWeight += entry.Weight;
			}

			if (totalWeight <= 0) return PlainChip;

			var roll = UnityEngine.Random.Range(0, totalWeight);

			foreach (var entry in _entries)
			{
				if (entry == null || !entry.Wagerable) continue;

				roll -= entry.Weight;
				if (roll < 0) return entry.Type;
			}

			return PlainChip;
		}

		public bool TryGetEntry(PokerItemType itemType, out Entry entry)
		{
			entry = null;

			if (itemType == PlainChip) return false;

			foreach (var candidate in _entries)
			{
				if (candidate == null || candidate.Type != itemType) continue;

				entry = candidate;
				return true;
			}

			return false;
		}
	}
}
