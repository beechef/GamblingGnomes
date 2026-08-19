using UnityEngine;

namespace Game.Runtime.Player
{
	// Something a player is putting up to be looked at, wherever it happens to be. A neck sent across the
	// table aims at a bone by default — but a mode that has laid something out to be read knows better
	// than the bone does, and this is how it says so without the neck having to know what it is.
	//
	// A pose rather than a point: where a head should end up and which way it should face when it gets
	// there are the same authoring decision, and splitting them leaves the second one to be guessed at in
	// code from angles nobody can picture.
	//
	// Answering false means "nothing on offer", and whatever asked falls back to what it would have done.
	public interface IPlayerLookPoint
	{
		bool TryGetLookPose(out Vector3 position, out Quaternion rotation);
	}
}
