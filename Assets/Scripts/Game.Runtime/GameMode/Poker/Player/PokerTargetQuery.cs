using System;
using Game.Runtime.UI.CursorVisuals;

namespace Game.Runtime.GameMode.Poker.Player
{
	// What a PokerTargetPointer may light up and hand back. Every filter left null offers nothing of that
	// kind, so a query names exactly the things its caller can use.
	public class PokerTargetQuery
	{
		public Func<PokerPlayer, bool> AcceptPlayer;
		public Func<PokerPlayer, int, bool> AcceptHoleCard;
		public Func<int, bool> AcceptBoardCard;

		// How the pointer looks while this is being asked.
		public CursorVisualState Cursor = CursorVisualState.Interact;
	}
}
