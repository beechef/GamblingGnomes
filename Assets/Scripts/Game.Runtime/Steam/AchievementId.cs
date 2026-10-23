namespace Game.Runtime.Steam
{
	// Each name is the achievement's API name in Steamworks, letter for letter: rename one here and on Steamworks
	// together. Numbered as the design sheet numbers them; they travel over RPC, so never renumber.
	public enum AchievementId : byte
	{
		JUST_A_TASTE = 1,
		TRIPPED_ON_A_TRIP = 2,
		OVERDOSE = 3,
		LAST_GNOME_STANDING = 4,
		DENIED_DEATH = 5,
		SURVIVOR = 6,
		DEAD_MAN_WINNING = 7,
		ITEM_ADDICT = 8,
		COMPLETIONIST = 9,
		STRATEGIST = 10,
		CAN_NOT_BE_SAVED = 11,
		FOUND_THE_CLOWN = 12,
		CHEERS_TO_THE_GRAVE = 13,
		BAD_TRIP = 14,
		LUCK_OR_SKILL = 15,
		THE_ULTIMATE_HAND = 16,
		THE_ROYAL_FAMILY = 17,
		SKILL_ISSUE = 18,
		LUCK_ISSUE = 19,
		NO_CARD_NEEDED = 20
	}
}
