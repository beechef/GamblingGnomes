using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Runtime.GameMode.Poker.Hallucination
{
	// A hallucination drawn as post-processing. The profile is the whole effect, so a new one is an
	// artist making a Volume Profile rather than anybody writing code — which is the point of the rungs
	// being pools: twelve of these and the ladder is full.
	[CreateAssetMenu(fileName = "Hallucination_Volume", menuName = "Game/Poker/Hallucination/Volume")]
	public class PokerHallucinationVolumeEffect : PokerHallucinationEffect
	{
		[Tooltip("What the world looks like at this rung.")]
		[Required]
		[SerializeField] private VolumeProfile _profile;

		[Tooltip("How strong it gets. Below one, several rungs can stack without the screen going white.")]
		[PropertyRange(0f, 1f)]
		[SerializeField] private float _weight = 0.6f;

		[Tooltip("Seconds it takes to arrive. A hallucination that snaps on reads as a bug rather than as a symptom.")]
		[SerializeField] private float _fadeDuration = 1.5f;

		[Tooltip("Where this rung sits against the others. Higher rungs should sit above lower ones.")]
		[SerializeField] private int _priority = 10;

		public VolumeProfile Profile => _profile;
		public float Weight => _weight;
		public float FadeDuration => Mathf.Max(0f, _fadeDuration);
		public int Priority => _priority;

		protected override PokerHallucinationEffectBehaviour Attach(GameObject host) => host.AddComponent<PokerHallucinationVolumeBehaviour>();
	}
}
