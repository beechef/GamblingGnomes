using System;
using UnityEngine;

namespace Game.Runtime.Interaction
{
	[Serializable]
	public struct SeatPose
	{
		[Tooltip("Animator state cross-faded while the pose is held. Left empty, the animator is untouched.")]
		public string AnimationState;

		[Tooltip("Off, the player is frozen facing the anchor — a bed or a locked-in chair.")]
		public bool AllowRotation;

		[Tooltip("Degrees the player may turn left/right of the anchor's forward.")]
		public Vector2 YawLimits;

		public Vector2 PitchLimits;

		public static SeatPose Default => new()
		{
			AnimationState = string.Empty,
			AllowRotation = true,
			YawLimits = new Vector2(-80f, 80f),
			PitchLimits = new Vector2(-60f, 60f)
		};
	}
}
