using Game.Runtime.Props;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Hallucination
{
	// Asks whatever the target names to wear one of its own looks. The prop decides what that look is, so
	// one effect aimed at the staked caps makes every kind of mushroom smile in its own way — where a
	// prefab swap would have to name one model and would be wrong for the next kind somebody authors.
	//
	// The target is also what says when the set moved: caps are spawned as they are wagered and destroyed
	// when the pot clears, so this re-scans on that event rather than resolving once.
	[CreateAssetMenu(fileName = "Hallucination_Variant", menuName = "Game/Poker/Hallucination/Variant")]
	public class PokerHallucinationVariantEffect : PokerHallucinationEffect
	{
		[Required]
		[SerializeField] private PokerHallucinationTarget _target;

		[Tooltip("Which of its looks each prop is asked for. A prop that has not authored this one is left as it was.")]
		[SerializeField] private PropVariant _variant = PropVariant.Smiling;

		public PokerHallucinationTarget Target => _target;
		public PropVariant Variant => _variant;

		protected override PokerHallucinationEffectBehaviour Attach(GameObject host) => host.AddComponent<PokerHallucinationVariantBehaviour>();
	}
}
