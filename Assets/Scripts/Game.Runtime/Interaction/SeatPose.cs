using System;
using UnityEngine;

namespace Game.Runtime.Interaction
{
	// How a body sits in this seat. Only the pose: how far the head may turn once seated is
	// PlayerController's, the one place a look limit is set.
	[Serializable]
	public struct SeatPose
	{
		[Tooltip("Animator state cross-faded while the pose is held. Left empty, the animator is untouched.")]
		public string AnimationState;

		public static SeatPose Default => new() { AnimationState = string.Empty };
	}
}
