using System;
using System.Collections.Generic;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Items
{
	// The prop every screen shows when an item is played: a magnifier over the board, a gun at the target,
	// a pill at a mouth. One row per item; an item with no row shows nothing. Pure presentation.
	[CreateAssetMenu(fileName = "PokerItemPropDatabase", menuName = "Game/Poker/Items/Item Prop Database")]
	public class PokerItemPropDatabase : ScriptableObject
	{
		[Serializable]
		public struct Entry
		{
			public PokerItemType Item;

			[Required]
			public GameObject Prefab;

			public PokerItemPropPlace Place;

			[Tooltip("Starts at the user and flies to the place, instead of appearing there.")]
			public bool FromUser;

			[Tooltip("Turns to face the target (its forward points at them).")]
			public bool FaceTarget;

			[Tooltip("World offset from the place's anchor where the prop appears (ignored with From User).")]
			public Vector3 StartOffset;

			[Tooltip("World offset from the place's anchor where the prop ends its move.")]
			public Vector3 EndOffset;

			[MinValue(0f)] public float AppearDuration;
			[MinValue(0f)] public float MoveDuration;
			[MinValue(0f)] public float HoldDuration;
			[MinValue(0f)] public float VanishDuration;

			public Ease MoveEase;
		}

		[SerializeField] private List<Entry> _entries = new();

		public bool TryGet(PokerItemType item, out Entry entry)
		{
			foreach (var candidate in _entries)
			{
				if (candidate.Item != item || !candidate.Prefab) continue;

				entry = candidate;
				return true;
			}

			entry = default;
			return false;
		}
	}
}
