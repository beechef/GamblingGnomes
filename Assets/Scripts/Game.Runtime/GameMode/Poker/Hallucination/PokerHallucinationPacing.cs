using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Video;

namespace Game.Runtime.GameMode.Poker.Hallucination
{
	// The blink that hides a rung change, and how long the effects take to ease in behind it, in one place:
	// the two only work together. The eye has to stay shut until the slowest effect has finished moving, so
	// the hold is never allowed to be shorter than the ease, whatever is typed into it.
	//
	// The blink is a video of the eyelids, and its own frames say when: it closes up to Shut Frame, the
	// rungs land there, the video waits on that frame for the hold, then plays on from Reopen Frame to its
	// end. So the effects switch on exactly as the lids meet, and retiming the blink is choosing two frames.
	//
	// Everything pacing a beat around a rung change (the eating, the roll, a death) asks the player's
	// hallucination controller, which reads this.
	[CreateAssetMenu(fileName = "PokerHallucinationPacing", menuName = "Game/Poker/Hallucination Pacing")]
	public class PokerHallucinationPacing : ScriptableObject
	{
		[Header("Blink")]
		[Tooltip("The eyelids, black where they are and light where the eye sees through. Empty applies a rung change outright with no blink.")]
		[SerializeField] private VideoClip _blinkVideo;

		[Tooltip("First frame the lids are fully shut. The rungs land on it.")]
		[MinValue(0)]
		[SerializeField] private int _shutFrame = 31;

		[Tooltip("Last frame the lids are fully shut. After the hold the video plays on from here.")]
		[MinValue(0)]
		[SerializeField] private int _reopenFrame = 38;

		[Tooltip("How fast the lids close and open against the video's own rate. 1.5 is half as fast again. The hold is not scaled.")]
		[MinValue(0.1)]
		[SerializeField] private float _playbackSpeed = 1f;

		[Tooltip("Seconds the eye stays shut after the change lands. Never shorter than Effect Ease, so the eye cannot open on an effect still moving.")]
		[SerializeField, Min(0f)] private float _hold = 0.8f;

		[Header("Effects")]
		[Tooltip("Seconds every effect takes to ease in or out: heads growing, a room or screen effect fading.")]
		[SerializeField, Min(0f)] private float _effectEase = 0.7f;

		public float EffectEase => _effectEase;

		public VideoClip BlinkVideo => _blinkVideo;

		private bool HasBlink => _blinkVideo && _blinkVideo.frameRate > 0d;

		private float FrameTime(int frame) =>
			HasBlink ? (float)(Mathf.Clamp(frame, 0, (int)_blinkVideo.frameCount) / _blinkVideo.frameRate) : 0f;

		private float Speed => Mathf.Max(_playbackSpeed, 0.1f);

		// Shut Time and Reopen Time are points in the video; the durations are seconds on the clock.
		public float ShutTime => FrameTime(_shutFrame);

		public float ReopenTime => FrameTime(Mathf.Max(_reopenFrame, _shutFrame));

		public float CloseDuration => ShutTime / Speed;

		public float HoldDuration => HasBlink ? Mathf.Max(_hold, _effectEase) : 0f;

		public float OpenDuration => HasBlink ? ((float)_blinkVideo.length - ReopenTime) / Speed : 0f;

		// The whole beat: closing, held shut, opening.
		public float TransitionDuration => CloseDuration + HoldDuration + OpenDuration;
	}
}
