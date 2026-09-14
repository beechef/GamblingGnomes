using Game.Runtime.Player;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Hallucination
{
	// Switches every body the target names to another version of whatever it is wearing — the table's
	// cartoon gnomes turning real in front of you.
	[CreateAssetMenu(fileName = "Hallucination_LookVersion", menuName = "Game/Poker/Hallucination/Look Version")]
	public class PokerHallucinationLookVersionEffect : PokerHallucinationAppearanceEffect
	{
		[Tooltip("The version each body is switched to. A model or outfit with no look for it stays in Cartoon.")]
		[SerializeField] private PlayerLookVersion _version = PlayerLookVersion.Realistic;

		public PlayerLookVersion Version => _version;

		protected override PokerHallucinationEffectBehaviour Attach(GameObject host) => host.AddComponent<PokerHallucinationLookVersionBehaviour>();
	}
}
