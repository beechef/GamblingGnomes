using Game.Runtime.Player;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Hallucination
{
	// Draws every body the target names as another model — the table turning into mushroom people. The
	// outfit and version each body is wearing carry across; the model decides what they mean on it.
	[CreateAssetMenu(fileName = "Hallucination_Model", menuName = "Game/Poker/Hallucination/Model")]
	public class PokerHallucinationModelEffect : PokerHallucinationAppearanceEffect
	{
		[Required]
		[SerializeField] private PlayerModel _model;

		public PlayerModel Model => _model;

		protected override PokerHallucinationEffectBehaviour Attach(GameObject host) => host.AddComponent<PokerHallucinationModelBehaviour>();
	}
}
