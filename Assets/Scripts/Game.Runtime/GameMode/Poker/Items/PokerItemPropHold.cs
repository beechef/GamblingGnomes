namespace Game.Runtime.GameMode.Poker.Items
{
	// What a prop waits on between its intro and its outro.
	public enum PokerItemPropHold : byte
	{
		Duration = 0,

		// Until the item has finished resolving: an answer, a card flight, a roll.
		UntilResolved = 1,

		// Until the fold lock the item laid on the performer has run out: handcuffs that pop open.
		UntilFoldLockLifted = 2
	}
}
