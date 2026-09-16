using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.AnimationVfx
{
	// Which effects a clip fires, and when. Edited through Tools > Animation VFX Preview, where the frame
	// can be seen. Installing and cleaning up the events is AnimationCueDatabase's half.
	[CreateAssetMenu(fileName = "AnimationVfxCueDatabase", menuName = "Game/Animation/VFX Cue Database")]
	public class AnimationVfxCueDatabase : AnimationCueDatabase
	{
		[Serializable]
		public class ClipCues
		{
			[Required]
			public AnimationClip Clip;

			public List<AnimationVfxCue> Cues = new();
		}

		[SerializeField] private List<ClipCues> _clips = new();

		public IReadOnlyList<ClipCues> Clips => _clips;

		public override string EventFunction => AnimationVfxPlayer.EventFunction;

		protected override IEnumerable<AnimationClip> TargetClips
		{
			get
			{
				foreach (var entry in _clips) yield return entry.Clip;
			}
		}

		public List<AnimationVfxCue> CuesFor(AnimationClip clip)
		{
			foreach (var entry in _clips)
			{
				if (entry.Clip == clip) return entry.Cues;
			}

			return null;
		}

		public AnimationVfxCue CueAt(AnimationClip clip, int index)
		{
			var cues = CuesFor(clip);
			return cues != null && index >= 0 && index < cues.Count ? cues[index] : null;
		}

		// The cue is named by its index rather than by the clip, so a player looks the row up again on the
		// clip the animator says is playing and an event some other database installed is never mistaken
		// for one of ours.
		protected override void BuildEvents(AnimationClip clip, List<AnimationEvent> into)
		{
			var cues = CuesFor(clip);
			if (cues == null) return;

			for (var i = 0; i < cues.Count; i++)
			{
				var cue = cues[i];
				if (cue == null || !cue.Prefab) continue;

				var animationEvent = CueEvent(clip, cue.TimeIn(clip));
				animationEvent.intParameter = i;
				animationEvent.objectReferenceParameter = this;

				into.Add(animationEvent);
			}
		}
	}
}
