using System;

namespace Game.Runtime.GameMode.Poker.Hands
{
	public readonly struct PokerHandResult : IComparable<PokerHandResult>
	{
		public readonly PokerHandType HandType;
		public readonly long Score;

		public static PokerHandResult None => new(null, -1);

		public bool IsValid => HandType;
		public string DisplayName => HandType ? HandType.DisplayName : string.Empty;

		public PokerHandResult(PokerHandType handType, long score)
		{
			HandType = handType;
			Score = score;
		}

		public int CompareTo(PokerHandResult other) => Score.CompareTo(other.Score);
	}
}
