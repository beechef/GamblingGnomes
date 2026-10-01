namespace Game.Runtime.GameMode.Poker.Items
{
	// Which board card a peek shows. The first two match PokerChoiceMode's values, which this field was
	// serialized as before the last face-down card became a choice.
	public enum PokerBoardPeekSlot : byte
	{
		Chosen = 0,
		Random = 1,
		LastFaceDown = 2
	}
}
