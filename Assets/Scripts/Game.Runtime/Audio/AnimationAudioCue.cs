using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.Audio
{
	// One sound fired at one frame of one clip, coming from one bone.
	[Serializable]
	public class AnimationAudioCue
	{
		[Tooltip("Frame of the clip the sound starts on, counted at the clip's own frame rate.")]
		[MinValue(0)]
		public int Frame;

		[Required]
		public AudioEvent Event;

		[Tooltip("Bone the sound comes from, by name, so one cue lands on whichever rig is drawn. Empty uses the rig's root.")]
		public string Bone;

		[Tooltip("Keeps following the bone while it plays. Off leaves it where it started.")]
		public bool FollowBone = true;

		public float TimeIn(AnimationClip clip) => clip && clip.frameRate > 0f ? Frame / clip.frameRate : 0f;
	}
}
