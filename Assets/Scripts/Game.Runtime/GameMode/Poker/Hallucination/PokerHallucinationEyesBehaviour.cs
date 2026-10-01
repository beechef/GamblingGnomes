using Game.Runtime.Player;

namespace Game.Runtime.GameMode.Poker.Hallucination
{
	public class PokerHallucinationEyesBehaviour : PokerHallucinationAppearanceBehaviour<PokerHallucinationEyesEffect>
	{
		protected override void Request(PlayerAppearanceController appearance) => appearance.SetEyes(this, Config.Eyes);

		protected override void Withdraw(PlayerAppearanceController appearance) => appearance.ClearEyes(this);
	}
}
