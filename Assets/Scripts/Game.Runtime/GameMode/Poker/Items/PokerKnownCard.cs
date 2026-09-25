using System;
using Unity.Netcode;

namespace Game.Runtime.GameMode.Poker.Items
{
	// One card somebody has been shown, and whose it was. The other client is a card's holder when it sits
	// on the viewer's list of what they know, and the viewer when it sits on the holder's list of what was
	// seen of theirs. NoTurn stands for the board. The card is kept as it was seen: a card swapped away
	// afterwards is still what they saw.
	public struct PokerKnownCard : INetworkSerializable, IEquatable<PokerKnownCard>
	{
		public ulong OtherClientId;
		public int Slot;
		public CardData Card;

		public bool IsBoard => OtherClientId == PokerGameData.NoTurn;

		public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
		{
			serializer.SerializeValue(ref OtherClientId);
			serializer.SerializeValue(ref Slot);
			serializer.SerializeValue(ref Card);
		}

		public bool Equals(PokerKnownCard other) => OtherClientId == other.OtherClientId && Slot == other.Slot && Card.Equals(other.Card);
	}
}
