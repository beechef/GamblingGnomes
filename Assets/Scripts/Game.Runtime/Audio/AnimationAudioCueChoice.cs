using System;
using UnityEngine;

namespace Game.Runtime.Audio
{
	// Decides, the moment a cue fires, whether it plays its alternate sound instead: the same frame of the same
	// clip can mean two things (snapping at yourself or at someone else). Picked per cue in the inspector, so a
	// new decision is a new subclass and no player code changes.
	[Serializable]
	public abstract class AnimationAudioCueChoice
	{
		// `rig` is the animator's object the cue fired on.
		public abstract bool UseAlternate(Transform rig);
	}
}
