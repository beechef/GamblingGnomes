using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Hallucination
{
	// The blink that hides a rung change, and how long the effects take to ease in behind it, in one place:
	// the two only work together. The eye has to stay shut until the slowest effect has finished moving, so
	// the hold is never allowed to be shorter than the ease, whatever is typed into it.
	//
	// Everything pacing a beat around a rung change (the eating, the roll, a death) asks the player's
	// hallucination controller, which reads this.
	[CreateAssetMenu(fileName = "PokerHallucinationPacing", menuName = "Game/Poker/Hallucination Pacing")]
	public class PokerHallucinationPacing : ScriptableObject
	{
		[Header("Blink")]
		[Tooltip("Seconds the eye spends closing and opening, together. Zero applies a rung change outright with no blink.")]
		[SerializeField, Min(0f)] private float _blinkDuration = 1f;

		[Tooltip("How much of the blink is spent closing. 0.5 closes and opens at the same speed.")]
		[SerializeField, Range(0f, 1f)] private float _closeShare = 0.5f;

		[Tooltip("Seconds the eye stays shut after the change lands. Never shorter than Effect Ease, so the eye cannot open on an effect still moving.")]
		[SerializeField, Min(0f)] private float _hold = 0.8f;

		[Header("Effects")]
		[Tooltip("Seconds every effect takes to ease in or out: heads growing, a room or screen effect fading.")]
		[SerializeField, Min(0f)] private float _effectEase = 0.7f;

		public float EffectEase => _effectEase;

		public float CloseDuration => _blinkDuration * _closeShare;

		public float HoldDuration => _blinkDuration > 0f ? Mathf.Max(_hold, _effectEase) : 0f;

		public float OpenDuration => _blinkDuration - CloseDuration;

		// The whole beat: closing, held shut, opening.
		public float TransitionDuration => _blinkDuration + HoldDuration;
	}
}
