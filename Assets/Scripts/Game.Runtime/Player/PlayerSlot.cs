namespace Game.Runtime.Player
{
	// A piece of the body a model or an outfit puts a mesh in. Each is one renderer in the player prefab,
	// on the one skeleton every model is exported against — so switching a model hands renderers new
	// meshes and never touches a bone, and everything hung on the rig stays where it is.
	//
	// A new piece is a new value here and a renderer in the prefab. The Hand values are the owner's
	// first-person rig; the rest are the body everybody else sees.
	public enum PlayerSlot
	{
		Body,
		Head,
		Outfit,
		Hat,
		HandBody,
		HandOutfit
	}
}
