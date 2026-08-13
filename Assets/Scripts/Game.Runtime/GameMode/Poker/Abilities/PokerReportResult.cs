using System;
using Unity.Netcode;

namespace Game.Runtime.GameMode.Poker.Abilities
{
	// Who is pointing at whom, replicated the moment the report is filed — the verdict comes later,
	// after the table has had its moment to sweat. Carries the sequence its verdict will answer to,
	// so a panel can tell "still thinking" from "already judged". Lives beside the verdict struct:
	// they are two halves of the same exchange.
	public struct PokerReportAccusation : INetworkSerializable, IEquatable<PokerReportAccusation>
	{
		public ulong AccuserClientId;
		public ulong TargetClientId;
		public int Sequence;

		public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
		{
			serializer.SerializeValue(ref AccuserClientId);
			serializer.SerializeValue(ref TargetClientId);
			serializer.SerializeValue(ref Sequence);
		}

		public bool Equals(PokerReportAccusation other)
		{
			return AccuserClientId == other.AccuserClientId
			       && TargetClientId == other.TargetClientId
			       && Sequence == other.Sequence;
		}
	}

	// The outcome of one accusation, replicated so every table can watch the verdict land. The
	// sequence number is what makes two identical verdicts in a row still read as two events.
	public struct PokerReportResult : INetworkSerializable, IEquatable<PokerReportResult>
	{
		public ulong AccuserClientId;
		public ulong TargetClientId;
		public bool WasCheater;
		public int Amount;
		public int Sequence;

		public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
		{
			serializer.SerializeValue(ref AccuserClientId);
			serializer.SerializeValue(ref TargetClientId);
			serializer.SerializeValue(ref WasCheater);
			serializer.SerializeValue(ref Amount);
			serializer.SerializeValue(ref Sequence);
		}

		public bool Equals(PokerReportResult other)
		{
			return AccuserClientId == other.AccuserClientId
			       && TargetClientId == other.TargetClientId
			       && WasCheater == other.WasCheater
			       && Amount == other.Amount
			       && Sequence == other.Sequence;
		}
	}
}
