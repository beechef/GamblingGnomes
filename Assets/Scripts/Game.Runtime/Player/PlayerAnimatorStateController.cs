using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Game.Runtime.Player
{
	// The seam between what a player *is* and what their rig is doing. Modes write named flags here and
	// the controller puts them on both animators; what any of them mean stays the caller's business, so
	// this piece carries no poker, no cards and no table.
	//
	// Replicated, because a pose is something the whole room watches: the owner renders the hand-only rig
	// and everyone else renders the body, and a flag written locally would pose one of the two. Server
	// writes, everyone reads, and a client arriving mid-hand applies the whole set as it stands rather
	// than waiting for the next change — which for a flag set before they joined is never.
	//
	// A parameter the controller does not have is skipped in silence, on purpose: a state can be driven
	// before the art that draws it has landed.
	public class PlayerAnimatorStateController : NetworkBehaviour
	{
		[Header("References")]
		[Tooltip("The rig everyone else renders.")]
		[SerializeField] private Animator _bodyAnimator;

		[Tooltip("The rig its owner renders.")]
		[SerializeField] private Animator _handOnlyAnimator;

		public readonly NetworkList<PlayerAnimatorState> States = new(null,
			NetworkVariableReadPermission.Everyone,
			NetworkVariableWritePermission.Server);

		public override void OnNetworkSpawn()
		{
			States.OnListChanged += HandleStatesChanged;

			ApplyAll();
		}

		public override void OnNetworkDespawn()
		{
			States.OnListChanged -= HandleStatesChanged;
		}

		// Idempotent, so a caller may write the same answer every time something changes without having to
		// remember what it wrote last — a list that only grows on repeats would be a leak with a pose on it.
		public void ServerSetBool(string parameter, bool value)
		{
			if (!IsServer || string.IsNullOrEmpty(parameter)) return;

			var name = new FixedString32Bytes(parameter);

			for (var i = 0; i < States.Count; i++)
			{
				if (!States[i].Parameter.Equals(name)) continue;

				if (States[i].Value == value) return;

				States[i] = new PlayerAnimatorState { Parameter = name, Value = value };
				return;
			}

			States.Add(new PlayerAnimatorState { Parameter = name, Value = value });
		}

		public bool GetBool(string parameter)
		{
			var name = new FixedString32Bytes(parameter);

			for (var i = 0; i < States.Count; i++)
			{
				if (States[i].Parameter.Equals(name)) return States[i].Value;
			}

			return false;
		}

		private void HandleStatesChanged(NetworkListEvent<PlayerAnimatorState> change)
		{
			switch (change.Type)
			{
				case NetworkListEvent<PlayerAnimatorState>.EventType.Add:
				case NetworkListEvent<PlayerAnimatorState>.EventType.Value:
					Apply(change.Value);
					break;

				default:
					ApplyAll();
					break;
			}
		}

		private void ApplyAll()
		{
			for (var i = 0; i < States.Count; i++) Apply(States[i]);
		}

		private void Apply(PlayerAnimatorState state)
		{
			Apply(_bodyAnimator, state);
			Apply(_handOnlyAnimator, state);
		}

		private static void Apply(Animator animator, PlayerAnimatorState state)
		{
			if (!animator || !animator.isActiveAndEnabled) return;

			var name = state.Parameter.ToString();

			foreach (var parameter in animator.parameters)
			{
				if (parameter.type != AnimatorControllerParameterType.Bool || parameter.name != name) continue;

				animator.SetBool(name, state.Value);
				return;
			}
		}
	}
}
