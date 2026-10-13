using System.Collections.Generic;

namespace Game.Runtime.GameMode.Poker.Items
{
	// The animation cues the item clips raise (events on Anim_Beer, Anim_Dice, …), shared by every item so a
	// step names the moment rather than a frame. Frames per clip are in .claude/rules/player-animation-camera.md.
	public static class PokerItemCues
	{
		// The hand reaches the prop under the table and takes it.
		public const string Grab = "ItemGrab";

		// The prop is put to use: lit, sniffed, pointed, injected, locked, poured.
		public const string Use = "ItemUse";

		// The hand lets the prop go: set down on the table, thrown, left on the other wrist.
		public const string Release = "ItemRelease";

		// The hand goes back under the table with the prop.
		public const string PutAway = "ItemPutAway";

		// The prop is gone without the hand: a pour dries up.
		public const string Vanish = "ItemVanish";

		public static IEnumerable<string> All
		{
			get
			{
				yield return Grab;
				yield return Use;
				yield return Release;
				yield return PutAway;
				yield return Vanish;
			}
		}
	}
}
