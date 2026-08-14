namespace Game.Runtime.Player
{
	// The bones anything outside a rig ever asks for, named after the part of the body they are instead
	// of after whatever the skeleton happens to call them.
	public enum PlayerBone
	{
		Root,
		Spine,
		Chest,
		Neck,
		Head,
		HeadTop,
		Jaw,
		ShoulderLeft,
		ShoulderRight,
		ElbowLeft,
		ElbowRight,
		HandLeft,
		HandRight,
		HipLeft,
		HipRight,
		KneeLeft,
		KneeRight,
		FootLeft,
		FootRight,
		ToeLeft,
		ToeRight
	}
}
