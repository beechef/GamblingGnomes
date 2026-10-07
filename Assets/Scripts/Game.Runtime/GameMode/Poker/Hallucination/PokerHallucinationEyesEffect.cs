using Game.Runtime.Player;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Hallucination
{
	// Another kind of eyes on every body the target names. Each model draws them its own way — an eye mask
	// per kind (PlayerModel.EyeMaskFor) — so this only names the kind.
	[CreateAssetMenu(fileName = "Hallucination_Eyes", menuName = "Game/Poker/Hallucination/Eyes")]
	public class PokerHallucinationEyesEffect : PokerHallucinationAppearanceEffect
	{
		[SerializeField] private PlayerEyeKind _eyes = PlayerEyeKind.Anime;

		public PlayerEyeKind Eyes => _eyes;

		protected override PokerHallucinationEffectBehaviour Attach(GameObject host) => host.AddComponent<PokerHallucinationEyesBehaviour>();
	}
}
