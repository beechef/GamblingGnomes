namespace Game.Runtime.GameMode.Poker.Items
{
	// One thing an item asks its user to point at before it is played.
	public enum PokerItemTargetKind : byte
	{
		Player = 0,
		OpponentCard = 1,
		OwnCard = 2,
		BoardCard = 3
	}
}
