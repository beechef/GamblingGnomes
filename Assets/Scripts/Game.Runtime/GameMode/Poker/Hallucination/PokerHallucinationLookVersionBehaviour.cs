using Game.Runtime.Player;

namespace Game.Runtime.GameMode.Poker.Hallucination
{
	public class PokerHallucinationLookVersionBehaviour : PokerHallucinationAppearanceBehaviour<PokerHallucinationLookVersionEffect>
	{
		protected override void Request(PlayerAppearanceController appearance) => appearance.SetVersion(this, Config.Version);

		protected override void Withdraw(PlayerAppearanceController appearance) => appearance.ClearVersion(this);
	}
}
