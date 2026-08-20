using Game.Runtime.GameMode.Config;

namespace Game.Runtime.UI.Config
{
	// The seam between the rows and wherever the values actually live. The in-game panel answers from
	// the replicated store and writes through the host RPC; the room screen answers from the pending
	// payload — the rows themselves never know which surface they are on.
	public interface IMatchConfigValueAccess
	{
		float GetValue(MatchConfigEntry entry);

		void SetValue(MatchConfigEntry entry, float value);
	}
}
