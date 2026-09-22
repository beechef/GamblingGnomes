using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Items
{
	// How many caps one mouthful takes off a plate. Handed to the eater by whoever asks them to eat, so two
	// tables can serve the same player differently.
	[Serializable]
	public class PokerBiteRule
	{
		[Tooltip("Caps one eating gesture swallows. The animation is one mouthful however many go into it, so this is how many a mouthful is.")]
		[MinValue(1)]
		[SerializeField] private int _itemsPerBite = 1;

		[Tooltip("On, a player's whole plate goes down in one mouthful, except the kinds eaten on their own, which follow one per mouthful. Items Per Bite is not used then.")]
		[SerializeField] private bool _eatWholePlate;

		[Tooltip("Kinds never swallowed with anything else while the whole plate is eaten at once: each is its own mouthful, after the rest. Colorful, whose roll is its own beat.")]
		[ShowIf(nameof(_eatWholePlate))]
		[SerializeField] private List<PokerItemType> _eatenOnTheirOwn = new() { PokerItemType.Colorful };

		public int ItemsPerBite => Mathf.Max(1, _itemsPerBite);
		public bool EatWholePlate => _eatWholePlate;

		public bool IsEatenOnItsOwn(PokerItemType itemType) => _eatenOnTheirOwn.Contains(itemType);
		public bool IsEatenWithOthers(PokerItemType itemType) => !_eatenOnTheirOwn.Contains(itemType);
	}
}
