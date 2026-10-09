using System;
using System.Collections.Generic;
using DG.Tweening;
using Game.Runtime.Audio;
using Game.Runtime.GameMode.Poker.Items;
using Game.Runtime.Player;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Player
{
	// One beat of an item performance: where the prop goes, how, and what the performer does as it starts.
	[Serializable]
	public struct PokerItemPerformanceStep
	{
		[Tooltip("Editor label only.")]
		public string Label;

		[MinValue(0f)]
		public float Duration;

		[Tooltip("Where the prop goes over the step, read every frame so a moving hand or face is followed.")]
		public PokerItemPropAnchor To;

		[Tooltip("Offset from the anchor in the performer's frame: x their right, y up, z towards the table.")]
		public Vector3 Offset;

		[Tooltip("How the prop is turned at the end of the step, in the performer's frame.")]
		public Vector3 Rotation;

		[Tooltip("Points the prop's forward at the other party (the knife at the target), then applies Rotation.")]
		public bool AimAtOther;

		[Tooltip("Degrees the prop turns about its own axes over the step, on top of Rotation: a die tumbling, a spatula flipping.")]
		public Vector3 Spin;

		public Ease Ease;

		[Tooltip("Keeps the prop turned the way the last step left it (a die still showing the face it landed on); Rotation and Aim At Other are ignored.")]
		public bool HoldRotation;

		[Tooltip("Shrinks the prop to nothing over the step. The prop starts at nothing, so the first step that should show it leaves this off.")]
		public bool Hide;

		[Tooltip("The performer's right hand reaches for the prop over the step: the stand-in until a clip carries the hand. Ignored while the prop rides the hand.")]
		public bool HandFollows;

		[Tooltip("Gesture the performer plays as the step starts. Skipped while the rig has no such state, so it can be named before its clip lands.")]
		[ValueDropdown(nameof(GestureIds))]
		public string Gesture;

		[Tooltip("Animator state played on the prop as the step starts (a lid springing open, a crank turning). Skipped when the prop has no such state.")]
		public string PropState;

		[Tooltip("Spawned on the prop as the step starts and destroyed after Effect Lifetime (or with the prop).")]
		public GameObject Effect;

		[MinValue(0f)]
		public float EffectLifetime;

		[Tooltip("Heard from the prop as the step starts.")]
		public AudioEvent Sound;

		[Tooltip("Only played when the item came out this way (outro steps of a chance item: a nice smell or a foul one).")]
		public bool OnlyOnOutcome;

		[ShowIf(nameof(OnlyOnOutcome))]
		[Tooltip("The outcome the item announced (PokerItem.GetOutcomeVerb's number).")]
		public int Outcome;

		private static IEnumerable<string> GestureIds
		{
			get
			{
				yield return string.Empty;
				foreach (var id in PlayerActionIds.All) yield return id;
			}
		}
	}
}
