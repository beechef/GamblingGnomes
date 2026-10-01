using Game.Runtime.Player;

namespace Game.Runtime.GameMode.Poker.Hallucination
{
	public class PokerHallucinationHideSlotsBehaviour : PokerHallucinationAppearanceBehaviour<PokerHallucinationHideSlotsEffect>
	{
		protected override void Request(PlayerAppearanceController appearance)
		{
			foreach (var slot in Config.Slots) appearance.SetSlotHidden(this, slot, true);
		}

		protected override void Withdraw(PlayerAppearanceController appearance)
		{
			foreach (var slot in Config.Slots) appearance.SetSlotHidden(this, slot, false);
		}
	}
}
