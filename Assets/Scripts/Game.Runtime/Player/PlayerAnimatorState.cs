using System;
using Unity.Collections;
using Unity.Netcode;

namespace Game.Runtime.Player
{
	// One animator flag, named rather than typed: the set a mode wants to drive is data, so a new state
	// is a call rather than a field on a controller everybody shares. The name travels because both rigs
	// resolve it themselves — a client renders one of the two and the other has to be posed identically
	// for whoever is looking at it.
	public struct PlayerAnimatorState : INetworkSerializable, IEquatable<PlayerAnimatorState>
	{
		public FixedString32Bytes Parameter;
		public bool Value;

		public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
		{
			serializer.SerializeValue(ref Parameter);
			serializer.SerializeValue(ref Value);
		}

		public bool Equals(PlayerAnimatorState other) => Parameter.Equals(other.Parameter) && Value == other.Value;

		public override bool Equals(object obj) => obj is PlayerAnimatorState other && Equals(other);

		public override int GetHashCode() => HashCode.Combine(Parameter, Value);
	}
}
