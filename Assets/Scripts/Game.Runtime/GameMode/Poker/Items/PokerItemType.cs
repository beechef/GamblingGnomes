namespace Game.Runtime.GameMode.Poker.Items
{
	// Which item a card in somebody's hand is. It replicates, so a value is never renumbered and a retired
	// item's number is never reused; the database row names its type rather than taking it from its position.
	public enum PokerItemType : byte
	{
		None = 0,
		Spatula = 2,
		Knife = 5,
		Vomit = 7,
		Syringe = 8,
		Beer = 9,
		Cigarette = 12,
		Dice = 13,
		Handcuffs = 17,
		Panties = 18,
		Megaphone = 19,
		JackBox = 20
	}
}
