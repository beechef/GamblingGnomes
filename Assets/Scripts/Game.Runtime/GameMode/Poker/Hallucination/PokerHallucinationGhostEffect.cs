using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.VFX;

namespace Game.Runtime.GameMode.Poker.Hallucination
{
	// Every body the target names sheds wisps: particles born inside the box it is drawn in that rise and fade,
	// as if its soul were leaving it. One VisualEffect per body, fed that box every frame.
	[CreateAssetMenu(fileName = "Hallucination_Ghost", menuName = "Game/Poker/Hallucination/Ghost")]
	public class PokerHallucinationGhostEffect : PokerHallucinationEffect
	{
		[Tooltip("Whose bodies. A player target whose bone is the root, so the whole rig sheds.")]
		[Required]
		[SerializeField] private PokerHallucinationTarget _target;

		[Tooltip("The graph. Exposes Center and Size (the body's world box), Rate (float) and Tint (Color).")]
		[Required]
		[SerializeField] private VisualEffectAsset _visualEffect;

		[Tooltip("Particles a second off one whole body.")]
		[MinValue(0f)]
		[SerializeField] private float _rate = 240f;

		[SerializeField] private Color _tint = new(0.55f, 0.9f, 1f, 1f);

		[Tooltip("Seconds the last wisps live after the effect stops, so they fade out instead of vanishing.")]
		[MinValue(0f)]
		[SerializeField] private float _wispLifetime = 1.6f;

		public PokerHallucinationTarget Target => _target;
		public VisualEffectAsset VisualEffect => _visualEffect;
		public float Rate => _rate;
		public Color Tint => _tint;
		public float WispLifetime => _wispLifetime;

		protected override PokerHallucinationEffectBehaviour Attach(GameObject host) => host.AddComponent<PokerHallucinationGhostBehaviour>();
	}
}
