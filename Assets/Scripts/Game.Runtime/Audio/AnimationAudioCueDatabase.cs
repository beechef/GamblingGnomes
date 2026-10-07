using System;
using System.Collections.Generic;
using Game.Runtime.AnimationVfx;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.Audio
{
	// Which sounds a clip fires, and when. Edited through Tools > Animation Cue Preview, where the frame can
	// be seen and heard. Installing and cleaning up the events is AnimationCueDatabase's half.
	[CreateAssetMenu(fileName = "AnimationAudioCueDatabase", menuName = "Game/Animation/Audio Cue Database")]
	public class AnimationAudioCueDatabase : AnimationCueDatabase
	{
		[Serializable]
		public class ClipCues
		{
			[Required]
			public AnimationClip Clip;

			public List<AnimationAudioCue> Cues = new();
		}

		[SerializeField] private List<ClipCues> _clips = new();

		public IReadOnlyList<ClipCues> Clips => _clips;

		public override string EventFunction => AnimationAudioPlayer.EventFunction;

		protected override IEnumerable<AnimationClip> TargetClips
		{
			get
			{
				foreach (var entry in _clips) yield return entry.Clip;
			}
		}

		public List<AnimationAudioCue> CuesFor(AnimationClip clip)
		{
			foreach (var entry in _clips)
			{
				if (entry.Clip == clip) return entry.Cues;
			}

			return null;
		}

		public AnimationAudioCue CueAt(AnimationClip clip, int index)
		{
			var cues = CuesFor(clip);
			return cues != null && index >= 0 && index < cues.Count ? cues[index] : null;
		}

		protected override void BuildEvents(AnimationClip clip, List<AnimationEvent> into)
		{
			var cues = CuesFor(clip);
			if (cues == null) return;

			for (var i = 0; i < cues.Count; i++)
			{
				var cue = cues[i];
				if (cue == null || !cue.Event) continue;

				var animationEvent = CueEvent(clip, cue.TimeIn(clip));
				animationEvent.intParameter = i;
				animationEvent.objectReferenceParameter = this;

				into.Add(animationEvent);
			}
		}
	}
}
