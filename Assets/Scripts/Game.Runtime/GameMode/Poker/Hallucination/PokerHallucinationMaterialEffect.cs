using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Hallucination
{
	// Repaints whatever the target names. Covers both the deck's player shader and its card shader, because
	// the difference between them is which target the asset points at rather than anything this does.
	[CreateAssetMenu(fileName = "Hallucination_Material", menuName = "Game/Poker/Hallucination/Material")]
	public class PokerHallucinationMaterialEffect : PokerHallucinationEffect
	{
		[Required]
		[SerializeField] private PokerHallucinationTarget _target;

		[Tooltip("What everything the target names is painted with.")]
		[Required]
		[SerializeField] private Material _material;

		public PokerHallucinationTarget Target => _target;
		public Material Material => _material;

		protected override PokerHallucinationEffectBehaviour Attach(GameObject host) => host.AddComponent<PokerHallucinationMaterialBehaviour>();
	}
}
