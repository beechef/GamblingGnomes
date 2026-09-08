using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Hallucination
{
	// A head that swells, or a body that shrinks. Written through PlayerBoneScaleController rather than
	// onto the bone, because every clip carries a constant scale curve for the whole skeleton and the
	// Animator would erase a direct write on the next frame.
	[CreateAssetMenu(fileName = "Hallucination_Scale", menuName = "Game/Poker/Hallucination/Scale")]
	public class PokerHallucinationScaleEffect : PokerHallucinationEffect
	{
		[Tooltip("Whose bone. A player target with a bone picked is what makes this one part rather than a whole body.")]
		[Required]
		[SerializeField] private PokerHallucinationTarget _target;

		[Tooltip("Multiplied onto the bone's authored scale. Two is twice the size, half is half.")]
		[SerializeField] private Vector3 _multiplier = new(2f, 2f, 2f);

		[Tooltip("Seconds it takes to grow. Snapping reads as a bug rather than as a symptom.")]
		[SerializeField] private float _duration = 1.2f;

		[SerializeField] private Ease _ease = Ease.OutBack;

		public PokerHallucinationTarget Target => _target;
		public Vector3 Multiplier => _multiplier;
		public float Duration => Mathf.Max(0f, _duration);
		public Ease Ease => _ease;

		protected override PokerHallucinationEffectBehaviour Attach(GameObject host) => host.AddComponent<PokerHallucinationScaleBehaviour>();
	}
}
