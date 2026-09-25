using Game.Runtime.GameMode.Poker.Items;
using Unity.Netcode;

namespace Game.Runtime.GameMode.Poker
{
	public enum PokerNoticeKind : byte
	{
		Action = 0,
		ItemUsed = 1,
		ItemBonus = 2,

		// What an item did to the one player it is told to, in detail: which of their cards was seen, and by whom.
		ItemDetail = 3,

		// The first hand of a match is about to be dealt. Amount is how many are playing it.
		MatchStarted = 4
	}

	// Something the table is told about, as data rather than as words: who, what, at whom, how many. The
	// view picks the wording and the shape, so a notice can never arrive already phrased for the wrong row.
	public struct PokerNotice : INetworkSerializable
	{
		public PokerNoticeKind Kind;
		public bool IsPrivate;
		public ulong ActorClientId;
		public ulong TargetClientId;
		public PokerActionType Action;
		public PokerItemType Item;
		public int Amount;
		public CardData Card;

		public bool HasTarget => TargetClientId != PokerGameData.NoTurn;

		public static PokerNotice ForAction(ulong actor, PokerActionType action) => new()
		{
			Kind = PokerNoticeKind.Action,
			ActorClientId = actor,
			TargetClientId = PokerGameData.NoTurn,
			Action = action
		};

		public static PokerNotice ForItemUsed(ulong actor, PokerItemType item, ulong target = PokerGameData.NoTurn) => new()
		{
			Kind = PokerNoticeKind.ItemUsed,
			ActorClientId = actor,
			TargetClientId = target,
			Item = item
		};

		public static PokerNotice ForItemDetail(ulong actor, PokerItemType item, CardData card) => new()
		{
			Kind = PokerNoticeKind.ItemDetail,
			ActorClientId = actor,
			TargetClientId = PokerGameData.NoTurn,
			Item = item,
			Card = card
		};

		public static PokerNotice ForItemBonus(ulong actor, int amount) => new()
		{
			Kind = PokerNoticeKind.ItemBonus,
			ActorClientId = actor,
			TargetClientId = PokerGameData.NoTurn,
			Amount = amount
		};

		public static PokerNotice ForMatchStarted(int players) => new()
		{
			Kind = PokerNoticeKind.MatchStarted,
			ActorClientId = PokerGameData.NoTurn,
			TargetClientId = PokerGameData.NoTurn,
			Amount = players
		};

		public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
		{
			serializer.SerializeValue(ref Kind);
			serializer.SerializeValue(ref IsPrivate);
			serializer.SerializeValue(ref ActorClientId);
			serializer.SerializeValue(ref TargetClientId);
			serializer.SerializeValue(ref Action);
			serializer.SerializeValue(ref Item);
			serializer.SerializeValue(ref Amount);
			serializer.SerializeValue(ref Card);
		}
	}
}
