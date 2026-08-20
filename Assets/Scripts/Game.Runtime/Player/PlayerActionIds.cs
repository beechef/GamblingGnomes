namespace Game.Runtime.Player
{
	// The gesture vocabulary, one name per act. Ids live here so the stage that triggers one and the
	// database entry that maps it can never drift apart on a typo.
	public static class PlayerActionIds
	{
		public const string Idle = "Idle";
		public const string Fold = "Fold";
		public const string Bet = "Bet";
		public const string Report = "Report";
		public const string Reported = "Reported";
		public const string Laugh = "Laugh";
		public const string Disappointed = "Disappointed";
		public const string WearGlasses = "WearGlasses";
		public const string RemoveGlasses = "RemoveGlasses";
		public const string ShuffleCards = "ShuffleCards";
		public const string Drink = "Drink";
		public const string Spill = "Spill";
	}
}
