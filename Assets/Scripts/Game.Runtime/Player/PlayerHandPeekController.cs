using Unity.Netcode;
using UnityEngine;

namespace Game.Runtime.Player
{
	// Whether this player is lifting their held cards to look, or keeping them face down on the table.
	// Cosmetic only: the pose replicates so every seat watches the same lift for as long as it lasts,
	// and whatever peeking means — who reads what into it — is some mode's business, never this one's.
	public class PlayerHandPeekController : NetworkBehaviour
	{
		[Header("Animation")]
		[Tooltip("Bool parameter driven on both rigs. A controller without it is quietly skipped, so the act can be scripted before the animation lands.")]
		[SerializeField] private string _peekingParameter = "IsPeekingCards";

		[Header("References")]
		[SerializeField] private Animator _bodyAnimator;
		[SerializeField] private Animator _handOnlyAnimator;

		[HideInInspector] public NetworkVariable<bool> IsPeeking = new(false,
			readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);

		public override void OnNetworkSpawn()
		{
			IsPeeking.OnValueChanged += HandlePeekingChanged;

			// A client joining mid-peek reads the pose as it stands rather than waiting for it to change.
			ApplyPose(IsPeeking.Value);
		}

		public override void OnNetworkDespawn()
		{
			IsPeeking.OnValueChanged -= HandlePeekingChanged;
		}

		// The owner asks, the server answers: the flip is trivial, but the pose everyone sees is state,
		// and state is written in exactly one place.
		[Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
		public void TogglePeekRPC()
		{
			IsPeeking.Value = !IsPeeking.Value;
		}

		// For a mode that wants the cards back down — a hand ending, a seat being left.
		public void ServerSetPeeking(bool peeking)
		{
			if (!IsServer) return;

			IsPeeking.Value = peeking;
		}

		private void HandlePeekingChanged(bool previous, bool current) => ApplyPose(current);

		private void ApplyPose(bool peeking)
		{
			Apply(_bodyAnimator, peeking);
			Apply(_handOnlyAnimator, peeking);
		}

		private void Apply(Animator animator, bool peeking)
		{
			if (!animator || !animator.isActiveAndEnabled) return;

			foreach (var parameter in animator.parameters)
			{
				if (parameter.type != AnimatorControllerParameterType.Bool || parameter.name != _peekingParameter) continue;

				animator.SetBool(_peekingParameter, peeking);
				return;
			}
		}
	}
}
