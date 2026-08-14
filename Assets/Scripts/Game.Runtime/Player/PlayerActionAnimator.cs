using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Game.Runtime.Player
{
	// One replicated gesture at a time: the fold thrown, the table slammed, the glasses put on. The event
	// itself rides a sequence number so the same gesture twice in a row still plays twice.
	public struct PlayerActionAnimationEvent : INetworkSerializable, IEquatable<PlayerActionAnimationEvent>
	{
		public FixedString32Bytes Id;
		public int Sequence;

		public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
		{
			serializer.SerializeValue(ref Id);
			serializer.SerializeValue(ref Sequence);
		}

		public bool Equals(PlayerActionAnimationEvent other) => Id == other.Id && Sequence == other.Sequence;
	}

	// Plays a player's action gestures on whatever this client renders of them. The server names the act,
	// every peer performs it on both rigs — the full body the table watches and the hand-only pair the
	// owner sees — so an action reads the same from every chair. What an id looks like is entirely the
	// database's and the animator controller's business: a new action is a new entry and a new state,
	// not a new method here.
	public class PlayerActionAnimator : NetworkBehaviour
	{
		[Header("References")]
		[SerializeField] private PlayerActionAnimationDatabase _database;
		[SerializeField] private Animator _bodyAnimator;
		[SerializeField] private Animator _handOnlyAnimator;

		[HideInInspector] public NetworkVariable<PlayerActionAnimationEvent> Current = new(default,
			readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);

		public override void OnNetworkSpawn()
		{
			// Change-only on purpose: a late joiner receives the last gesture as spawned state, and a
			// gesture from half a hand ago is not worth replaying at them.
			Current.OnValueChanged += HandleCurrentChanged;
		}

		public override void OnNetworkDespawn()
		{
			Current.OnValueChanged -= HandleCurrentChanged;
		}

		public void ServerPlay(string actionId)
		{
			if (!IsServer || string.IsNullOrEmpty(actionId)) return;

			Current.Value = new PlayerActionAnimationEvent
			{
				Id = actionId,
				Sequence = Current.Value.Sequence + 1
			};
		}

		private void HandleCurrentChanged(PlayerActionAnimationEvent previous, PlayerActionAnimationEvent current)
		{
			Play(current.Id.ToString());
		}

		private void Play(string actionId)
		{
			if (!_database || !_database.TryFind(actionId, out var entry)) return;

			var stateName = string.IsNullOrEmpty(entry.StateName) ? entry.Id : entry.StateName;

			CrossFade(_bodyAnimator, stateName, entry);
			CrossFade(_handOnlyAnimator, stateName, entry);
		}

		// A state the controller does not have yet is quietly skipped, so gestures can be scripted ahead
		// of their animations landing — the same tolerance the seat poses get.
		private static void CrossFade(Animator animator, string stateName, PlayerActionAnimationDatabase.Entry entry)
		{
			if (!animator || !animator.isActiveAndEnabled) return;
			if (entry.Layer >= animator.layerCount) return;
			if (!animator.HasState(entry.Layer, Animator.StringToHash(stateName))) return;

			animator.CrossFade(stateName, Mathf.Max(0f, entry.CrossFade), entry.Layer);
		}
	}
}
