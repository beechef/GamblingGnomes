using System;
using Unity.Netcode;

namespace Game.Runtime.GameMode.Poker.Items
{
	// An item waiting on somebody other than its user: who has to answer, for whom, and until when. Public,
	// so the whole table can see who the game is waiting on; what they choose is only known once it lands.
	public struct PokerItemResponse : INetworkSerializable, IEquatable<PokerItemResponse>
	{
		public static readonly PokerItemResponse None = new() { ResponderClientId = PokerGameData.NoTurn, RequesterClientId = PokerGameData.NoTurn };

		public ulong ResponderClientId;
		public ulong RequesterClientId;
		public PokerItemType Item;
		public double EndTime;
		public float Duration;

		public bool IsPending => ResponderClientId != PokerGameData.NoTurn;

		public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
		{
			serializer.SerializeValue(ref ResponderClientId);
			serializer.SerializeValue(ref RequesterClientId);
			serializer.SerializeValue(ref Item);
			serializer.SerializeValue(ref EndTime);
			serializer.SerializeValue(ref Duration);
		}

		public bool Equals(PokerItemResponse other) =>
			ResponderClientId == other.ResponderClientId && RequesterClientId == other.RequesterClientId && Item == other.Item && EndTime.Equals(other.EndTime);
	}
}
