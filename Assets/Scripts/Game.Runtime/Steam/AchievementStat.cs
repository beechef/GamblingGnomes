namespace Game.Runtime.Steam
{
	// Each name is the stat's API name in Steamworks (type INT), letter for letter.
	public enum AchievementStat : byte
	{
		// One bit per item type the player has ever used, bit = (int)PokerItemType.
		ITEM_TYPES_USED = 1
	}
}
