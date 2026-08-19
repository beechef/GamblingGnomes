using System.Collections.Generic;

namespace Game.Runtime.GameMode.Config
{
	// Lets the room screen enumerate a mode prefab's tunables without knowing the mode's type — and
	// without instantiating anything: entries built off an asset are read for metadata and defaults
	// only, never applied.
	public interface IMatchConfigProvider
	{
		void CollectAuthoredConfigEntries(List<MatchConfigEntry> entries);
	}
}
