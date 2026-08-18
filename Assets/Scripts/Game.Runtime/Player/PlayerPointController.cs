using Unity.Netcode;
using UnityEngine;

namespace Game.Runtime.Player
{
	// Whether this player is holding an arm out at somebody. Cosmetic only, and a held pose rather than a
	// gesture: an accusation lasts as long as the accuser is deciding, so it cannot be a one-shot on
	// PlayerActionAnimator that ends whenever its clip does. What pointing means — who is being pointed
	// at, what it costs — is some mode's business, never this one's.
	public class PlayerPointController : NetworkBehaviour
	{
		[Header("Animation")]
		[Tooltip("Bool parameter driven on both rigs. A controller without it is quietly skipped, so the act can be scripted before the animation lands.")]
		[SerializeField] private string _pointingParameter = "IsPointing";

		[Header("References")]
		[SerializeField] private Animator _bodyAnimator;
		[SerializeField] private Animator _handOnlyAnimator;

		[Tooltip("Told to aim from the chest while the arm is out. An arm thrown across a table is a whole torso, so the look has to come off the bone that carries it.")]
		[SerializeField] private PlayerController _playerController;

		[HideInInspector] public NetworkVariable<bool> IsPointing = new(false,
			readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);

		public override void OnNetworkSpawn()
		{
			if (!_playerController) _playerController = GetComponentInParent<PlayerController>();

			IsPointing.OnValueChanged += HandlePointingChanged;

			// A client arriving mid-accusation reads the pose as it stands rather than waiting for it to change.
			ApplyPose(IsPointing.Value);
		}

		public override void OnNetworkDespawn()
		{
			IsPointing.OnValueChanged -= HandlePointingChanged;
		}

		public void ServerSetPointing(bool pointing)
		{
			if (!IsServer) return;

			IsPointing.Value = pointing;
		}

		private void HandlePointingChanged(bool previous, bool current) => ApplyPose(current);

		private void ApplyPose(bool pointing)
		{
			Apply(_bodyAnimator, pointing);
			Apply(_handOnlyAnimator, pointing);

			// Released rather than set back to a remembered value: what the look aims at the moment the arm
			// comes down is the seat's business, and it is the one that knows whether there is still a chair.
			if (!_playerController) return;

			if (pointing) _playerController.SetLookModeOverride(PlayerLookMode.Body);
			else _playerController.ClearLookModeOverride();
		}

		private void Apply(Animator animator, bool pointing)
		{
			if (!animator || !animator.isActiveAndEnabled) return;

			foreach (var parameter in animator.parameters)
			{
				if (parameter.type != AnimatorControllerParameterType.Bool || parameter.name != _pointingParameter) continue;

				animator.SetBool(_pointingParameter, pointing);
				return;
			}
		}
	}
}
