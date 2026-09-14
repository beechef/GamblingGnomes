using Game.Runtime.Player;

namespace Game.Runtime.GameMode.Poker.Hallucination
{
	public class PokerHallucinationModelBehaviour : PokerHallucinationAppearanceBehaviour<PokerHallucinationModelEffect>
	{
		protected override void Request(PlayerAppearanceController appearance) => appearance.SetModel(this, Config.Model);

		protected override void Withdraw(PlayerAppearanceController appearance) => appearance.ClearModel(this);
	}
}
