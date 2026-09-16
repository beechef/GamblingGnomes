namespace Game.Runtime.Player
{
	// One caller's say in how far a blend shape is pushed. Held by whoever asked for it rather than looked
	// up by a handle every frame: a tween easing a shape in writes straight into this, which is a field
	// write and not a search through everything else standing on the same shape.
	//
	// It is live — changing Weight is seen by the next frame — and it stops mattering the moment it is
	// removed. Nothing here decides anything: the controller reads it, and the caller owns it.
	public sealed class PlayerBlendShapeModifier
	{
		public float Weight;

		internal PlayerBlendShapeController Owner;
		internal string Shape;

		// Idempotent, so a caller that removes on both an end and a teardown does not have to remember
		// which of the two ran first.
		public void Remove()
		{
			var owner = Owner;
			Owner = null;

			if (owner) owner.Remove(this);
		}
	}
}
