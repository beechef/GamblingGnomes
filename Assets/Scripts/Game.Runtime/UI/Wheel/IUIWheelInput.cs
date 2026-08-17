using System;

namespace Game.Runtime.UI.Wheel
{
	// Where a turn of the wheel comes from. Behind an interface so the same wheel answers to keys, a
	// scroll wheel or a stick without knowing which — the controller only ever hears "one step, this way".
	public interface IUIWheelInput
	{
		event Action<UIWheelDirection> OnStepped;
	}
}
