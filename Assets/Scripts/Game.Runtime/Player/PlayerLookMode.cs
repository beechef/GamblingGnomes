namespace Game.Runtime.Player
{
	// Which bone the look composes onto. Named rather than left as a bool, because what decides it and
	// what it decides pulled apart the moment a third case turned up: a seated player pointing across the
	// table is anchored — the chair still holds their body — and yet aims from the chest, which no reading
	// of "anchored" can answer.
	public enum PlayerLookMode : byte
	{
		// Chest_M, which parents the neck chain and both scapulae — so looking down lowers the hands with
		// the gaze instead of craning a head off a body that never moved. What standing up looks like, and
		// what throwing an arm across the table looks like.
		Body = 0,

		// Head_M alone. A chair has already decided how the body sits, and swinging the whole torso to read
		// the table throws that pose away.
		Head = 1
	}
}
