using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Hallucination
{
	// Repaints whatever the target names. Covers both the deck's player shader and its card shader, because
	// the difference between them is which target the asset points at rather than anything this does.
	[CreateAssetMenu(fileName = "Hallucination_Material", menuName = "Game/Poker/Hallucination/Material")]
	public class PokerHallucinationMaterialEffect : PokerHallucinationEffect
	{
		public enum PaintMode
		{
			// Every slot on the renderer becomes this material. What a card looked like is gone while it
			// runs — the right answer for a body that has to read as made of something else.
			Replace,

			// Hung on the end of what the renderer is already wearing, as an extra pass. The card keeps its
			// face and the material draws over it, which is the only way a rank stays readable underneath.
			Add
		}

		[Required]
		[SerializeField] private PokerHallucinationTarget _target;

		[Tooltip("What everything the target names is painted with.")]
		[Required]
		[SerializeField] private Material _material;

		[Tooltip("Replace covers the deck's player shader. Add is what a card wants: the face stays and this draws on top of it.")]
		[SerializeField] private PaintMode _mode = PaintMode.Replace;

		public PokerHallucinationTarget Target => _target;
		public Material Material => _material;
		public PaintMode Mode => _mode;

		protected override PokerHallucinationEffectBehaviour Attach(GameObject host) => host.AddComponent<PokerHallucinationMaterialBehaviour>();
	}
}
