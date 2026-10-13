using System;
using System.Collections.Generic;
using DG.Tweening;
using Game.Runtime.GameMode.Poker.Items;
using Game.Runtime.Player;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Player
{
	// One thing an item performance does, and the moment it does it: mostly a cue the performer's clip raises,
	// so the animation drives the prop (shown at the grab, released on the table, its own clip played at use).
	[Serializable]
	public struct PokerItemPerformanceAction
	{
		[Tooltip("Editor label only.")]
		public string Label;

		public PokerItemMoment When;

		[ShowIf(nameof(When), PokerItemMoment.Cue)]
		[ValueDropdown(nameof(CueNames))]
		public string Cue;

		[ShowIf(nameof(When), PokerItemMoment.Outcome)]
		[Tooltip("The outcome the item announced (PokerItem.GetOutcomeVerb's number).")]
		public int Outcome;

		[Tooltip("Seconds after the moment before the action runs.")]
		[MinValue(0f)]
		public float Delay;

		public PokerItemPropAction Do;

		[ShowIf("@Do == PokerItemPropAction.AttachTo || Do == PokerItemPropAction.MoveTo || Do == PokerItemPropAction.AttachCardPoint || Do == PokerItemPropAction.AimHand")]
		public PokerItemPropAnchor Anchor;

		[ShowIf(nameof(Do), PokerItemPropAction.MoveTo)]
		[Tooltip("Offset from the anchor: in the palm's frame for a hand, else the chair's (x their right, y up, z towards the table).")]
		public Vector3 Offset;

		[ShowIf(nameof(Do), PokerItemPropAction.MoveTo)]
		[Tooltip("How the prop is turned at the end, in the same frame as Offset.")]
		public Vector3 Rotation;

		[ShowIf(nameof(Do), PokerItemPropAction.MoveTo)]
		[Tooltip("Height of the hop on the way, in metres: 0 travels straight.")]
		[MinValue(0f)]
		public float Arc;

		[ShowIf("@Do == PokerItemPropAction.Show || Do == PokerItemPropAction.Hide || Do == PokerItemPropAction.MoveTo")]
		[MinValue(0f)]
		public float Duration;

		[ShowIf("@Do == PokerItemPropAction.Show || Do == PokerItemPropAction.Hide || Do == PokerItemPropAction.MoveTo")]
		public Ease Ease;

		[ShowIf(nameof(Do), PokerItemPropAction.PlayState)]
		[Tooltip("State on the prop's animator. Skipped when the prop has no such state.")]
		public string State;

		[ShowIf(nameof(Do), PokerItemPropAction.PlayGesture)]
		[ValueDropdown(nameof(GestureIds))]
		public string Gesture;

		[ShowIf(nameof(Do), PokerItemPropAction.SpawnEffect)]
		public GameObject Effect;

		[ShowIf(nameof(Do), PokerItemPropAction.SpawnEffect)]
		[Tooltip("Where the effect is spawned and rides. Empty: the prop.")]
		public Transform EffectPoint;

		[ShowIf(nameof(Do), PokerItemPropAction.SpawnEffect)]
		[MinValue(0f)]
		public float EffectLifetime;

		public bool Matches(PokerItemMoment moment, string cue, int outcome) =>
			When == moment && (moment != PokerItemMoment.Cue || Cue == cue) && (moment != PokerItemMoment.Outcome || Outcome == outcome);

		private static IEnumerable<string> CueNames => PokerItemCues.All;

		private static IEnumerable<string> GestureIds => PlayerActionIds.All;
	}
}
