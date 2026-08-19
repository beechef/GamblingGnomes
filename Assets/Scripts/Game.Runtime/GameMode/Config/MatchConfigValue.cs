using System;
using Unity.Collections;
using Unity.Netcode;

namespace Game.Runtime.GameMode.Config
{
	public struct MatchConfigValue : INetworkSerializable, IEquatable<MatchConfigValue>
	{
		public FixedString64Bytes Id;
		public float Value;

		public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
		{
			serializer.SerializeValue(ref Id);
			serializer.SerializeValue(ref Value);
		}

		public bool Equals(MatchConfigValue other) => Id.Equals(other.Id) && Value.Equals(other.Value);

		public override bool Equals(object obj) => obj is MatchConfigValue other && Equals(other);

		public override int GetHashCode() => HashCode.Combine(Id, Value);
	}
}
