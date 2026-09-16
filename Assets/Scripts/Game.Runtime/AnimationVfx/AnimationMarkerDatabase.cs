using System;
using System.Collections.Generic;
using Game.Runtime.Player;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.AnimationVfx
{
	// Named moments in a clip — the frame a hand reaches the cards, the frame it lets go — as rows in an
	// asset rather than seconds counted in whatever code is waiting for them. A number in code is a guess
	// that goes stale the moment the animation is retimed, and the same beat in two clips needs two
	// numbers; a marker is one name that each clip answers for itself.
	//
	// The events go through the same relay a hand-authored one does (PlayerAnimationEventRelay), so a clip
	// the project owns can carry its markers in the Animation window and a clip inside an FBX gets them
	// from here — where an export cannot take them with it — and nothing listening can tell the difference.
	[CreateAssetMenu(fileName = "AnimationMarkerDatabase", menuName = "Game/Animation/Marker Database")]
	public class AnimationMarkerDatabase : AnimationCueDatabase
	{
		[Serializable]
		public struct Marker
		{
			[Tooltip("What this moment is called. Whoever waits for it asks by this name, so it is the one thing both sides have to agree on.")]
			public string Name;

			[Tooltip("Frame of the clip it lands on, counted at the clip's own frame rate.")]
			[MinValue(0)]
			public int Frame;
		}

		[Serializable]
		public class ClipMarkers
		{
			[Required]
			public AnimationClip Clip;

			public List<Marker> Markers = new();
		}

		[SerializeField] private List<ClipMarkers> _clips = new();

		public IReadOnlyList<ClipMarkers> Clips => _clips;

		public override string EventFunction => PlayerAnimationEventRelay.EventFunction;

		protected override IEnumerable<AnimationClip> TargetClips
		{
			get
			{
				foreach (var entry in _clips) yield return entry.Clip;
			}
		}

		// The name rides in the event's own string, so a listener needs nothing from this asset to read one
		// — which is what lets a marker installed here and one typed into the Animation window arrive the
		// same way.
		protected override void BuildEvents(AnimationClip clip, List<AnimationEvent> into)
		{
			foreach (var entry in _clips)
			{
				if (entry.Clip != clip) continue;

				foreach (var marker in entry.Markers)
				{
					if (string.IsNullOrEmpty(marker.Name)) continue;

					var time = clip.frameRate > 0f ? marker.Frame / clip.frameRate : 0f;

					var animationEvent = CueEvent(clip, time);
					animationEvent.stringParameter = marker.Name;

					into.Add(animationEvent);
				}
			}
		}
	}
}
