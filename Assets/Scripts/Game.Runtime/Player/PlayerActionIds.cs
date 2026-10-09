using System.Collections.Generic;
using System.Linq;
using System.Reflection;

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

		// Item performances (PokerItemPerformance). Scripted ahead of their clips: a state the rig lacks is skipped.
		public const string ItemTakeOut = "ItemTakeOut";
		public const string ItemPutAway = "ItemPutAway";
		public const string ItemThrowAway = "ItemThrowAway";
		public const string ItemSpatulaFlip = "ItemSpatulaFlip";
		public const string ItemKnifeThreaten = "ItemKnifeThreaten";
		public const string ItemVomit = "ItemVomit";
		public const string ItemPickUpCard = "ItemPickUpCard";
		public const string ItemSmoke = "ItemSmoke";
		public const string ItemDiceRoll = "ItemDiceRoll";
		public const string ItemJackBoxCrank = "ItemJackBoxCrank";
		public const string ItemSniff = "ItemSniff";
		public const string ItemSniffNice = "ItemSniffNice";
		public const string ItemSniffGross = "ItemSniffGross";
		public const string ItemInject = "ItemInject";
		public const string ItemDrink = "ItemDrink";
		public const string ItemMegaphone = "ItemMegaphone";
		public const string ItemCuffed = "ItemCuffed";
		public const string ItemCuffsBreak = "ItemCuffsBreak";

		// Every id above, for inspector dropdowns.
		public static IEnumerable<string> All =>
			typeof(PlayerActionIds)
				.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
				.Where(field => field.IsLiteral && field.FieldType == typeof(string))
				.Select(field => (string)field.GetRawConstantValue());
	}
}
