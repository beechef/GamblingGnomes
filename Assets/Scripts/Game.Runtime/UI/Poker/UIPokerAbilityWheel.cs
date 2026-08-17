using Game.Runtime.GameMode.Poker.Abilities;
using Game.Runtime.UI.Wheel;

namespace Game.Runtime.UI.Poker
{
	// The wheel closed over the one thing this game puts on it. Unity cannot place an open generic on a
	// GameObject, so the concrete type has to exist somewhere; there is nothing else for it to do.
	public class UIPokerAbilityWheel : UIWheelController<PokerAbility>
	{
	}
}
