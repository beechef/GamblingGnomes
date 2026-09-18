using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Stages
{
	// How the eating beat is paced, in one place. Everything the table waits on between one cap going down
	// and the next — the mouthful, the hit, a Colorful roll being swept and held — is a number here, and
	// nothing else in the round carries a copy of it. What a cap *costs* is not here (that is the effect
	// assets and the tiers ladder), and neither is what the bar looks like while it plays (that is the
	// meter prefab): this is only *when*.
	//
	// Owned by the stage that runs the beat, so two eating stages can be paced differently. The mouthful and
	// the impact are read off their clips. What a rung change or a death costs is not here: the blink and the
	// death each have their own pacing asset, and the stage asks the eater's controllers for them.
	[CreateAssetMenu(fileName = "PokerConsumePacing", menuName = "Game/Poker/Consume Pacing")]
	public class PokerConsumePacing : ScriptableObject
	{
		[Header("Mouthful")]
		[Tooltip("The eating animation. One mouthful lasts exactly as long as it, so the next bite never cuts it off.")]
		[SerializeField] private AnimationClip _biteClip;

		[Tooltip("Seconds of quiet between one cap going down and the same player starting the next, so a plate of three reads as three mouthfuls rather than one long one.")]
		[field: SerializeField, Min(0f)] public float GapBetweenBites { get; private set; }

		[Tooltip("On, the next mouthful waits out the eater's blink when the cap just eaten moved them across a hallucination rung. A bite landing inside the blink is a bite nobody saw.")]
		[field: SerializeField] public bool WaitForHallucinationTransition { get; private set; } = true;

		[Tooltip("Seconds between one player finishing their plate and the next starting theirs, so two players eating do not read as one.")]
		[field: SerializeField, Min(0f)] public float HandoverDuration { get; private set; } = 0.4f;

		[Header("Impact")]
		[Tooltip("The impact reaction a mouthful that lifts its eater onto a new rung plays. The table waits it out before the world changes.")]
		[SerializeField] private AnimationClip _impactClip;

		[Header("Colorful roll")]
		[Tooltip("Seconds between the cap's gain landing and the skull starting to move, so the bar has filled to the rate being rolled against. When the gain crossed a rung, the blink is waited out instead.")]
		[field: SerializeField, Min(0f)] public float RollLeadIn { get; private set; } = 0.6f;

		[Tooltip("Seconds the skull spends sweeping before it stops on the number.")]
		[field: SerializeField, Min(0.1f)] public float RollSweepDuration { get; private set; } = 2.5f;

		[Tooltip("Seconds the stopped skull holds on the result before anything follows — the death on a fatal roll, the next mouthful on a survived one. Feedback on the result plays inside it.")]
		[field: SerializeField, Min(0f)] public float RollResultHold { get; private set; } = 2f;

		public float BiteDuration => _biteClip ? Mathf.Max(0.1f, _biteClip.length) : 0.1f;

		public float ImpactDuration => _impactClip ? _impactClip.length : 0f;
	}
}
