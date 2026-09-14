using DG.Tweening;
using Game.Runtime.Player;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Serialization;

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

		[Tooltip("Per axis, so a head can be made tall without being made wide. What it does with the bone's authored scale is the mode below.")]
		[FormerlySerializedAs("_multiplier")]
		[SerializeField] private Vector3 _scale = new(2f, 2f, 2f);

		[Tooltip("How this joins whatever else is already on the bone. Multiply composes — a swell and a shrink running together cancel out rather than one erasing the other.")]
		[SerializeField] private PlayerBoneScaleMode _mode = PlayerBoneScaleMode.Multiply;

		[Tooltip("Seconds it takes to grow. Snapping reads as a bug rather than as a symptom.")]
		[SerializeField] private float _duration = 1.2f;

		[SerializeField] private Ease _ease = Ease.OutBack;

		public PokerHallucinationTarget Target => _target;
		public Vector3 Scale => _scale;
		public PlayerBoneScaleMode Mode => _mode;
		public float Duration => Mathf.Max(0f, _duration);
		public Ease Ease => _ease;

		protected override PokerHallucinationEffectBehaviour Attach(GameObject host) => host.AddComponent<PokerHallucinationScaleBehaviour>();
	}
}
