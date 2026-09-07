namespace Game.Runtime.GameMode.Poker
{
	// How a hand pays out. Three genuinely different answers rather than a pair of bools, which would
	// let two of them be switched on at once and leave the order they are read in deciding the game.
	public enum PokerSettlement : byte
	{
		// Poker: the strongest hand takes the pot.
		WinnerTakesPot = 0,

		// The pot is a plate. The weakest hand still in eats every unit of it, shared out.
		LoserEatsPot = 1,

		// The mushroom round: everyone who lost swallows a full copy of what the winner wagered, and a
		// player who folded eats only their own opening cap. Nothing is ever won.
		LosersEatWinnersWager = 2
	}
}
