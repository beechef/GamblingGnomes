namespace Game.Runtime.Player
{
	// The gesture vocabulary, one name per act. Ids live here so the stage that triggers one and the
	// database entry that maps it can never drift apart on a typo.
	public static class PlayerActionIds
	{
		public const string Idle = "Idle";
		public const string Fold = "Fold";
		public const string Bet = "Bet";
		public const string Laugh = "Laugh";
		public const string PickUpCard = "PickUpCard";
		public const string ConsumeItem = "ConsumeItem";
		public const string Impact = "Impact";
		public const string SnapFingers = "SnapFingers";
	}
}
