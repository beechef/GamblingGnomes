namespace Game.Runtime.GameMode.Poker
{
	// How a hand pays out. An enum rather than a bool, because a third answer has already existed once and
	// two bools would let two of them be switched on at the same time and leave the order they are read in
	// deciding the game.
	//
	// The numbers are explicit and the gap at 1 is deliberate: a preset on disk stores this as an integer,
	// so renumbering would silently make the mushroom round settle like poker.
	public enum PokerSettlement : byte
	{
		// Poker: the strongest hand takes the pot.
		WinnerTakesPot = 0,

		// The mushroom round: everyone who lost swallows a full copy of what the winner wagered, and a
		// player who folded eats only their own opening cap. Nothing is ever won.
		LosersEatWinnersWager = 2
	}
}
