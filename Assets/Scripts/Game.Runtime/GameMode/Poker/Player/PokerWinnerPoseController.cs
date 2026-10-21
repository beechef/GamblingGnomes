using Game.Runtime.Player;
using Unity.Netcode;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Player
{
	// The winner's celebration, held rather than played once. It lasts from the hand being scored until the
	// table comes round to the Colorful pick, which is a stretch of the round and not the length of a clip —
	// so it is a bool on the animator, the same shape every other held pose takes, and a gesture would end
	// whenever its own take happened to run out.
	//
	// The parameter is named here and nowhere else. The beats that raise and drop it are talking about a
	// player celebrating, not about a string on a controller, and two stages typing the same one is exactly
	// how a rename comes to move only half of them.
	public class PokerWinnerPoseController : NetworkBehaviour
	{
		[SerializeField] private PlayerAnimatorStateController _animatorStates;

		[Tooltip("Bool held on both rigs while this player is celebrating a hand they took.")]
		[SerializeField] private string _smilingParameter = "IsSmiling";

		[Tooltip("Bool held on both rigs while this player dances as the match's survivor.")]
		[SerializeField] private string _dancingParameter = "IsDancing";

		[Header("Stage")]
		[Tooltip("What is lifted onto the table for the victory dance: the models, both rigs, never the root the seat and the network own.")]
		[SerializeField] private Transform _modelRoot;

		[SerializeField] private PlayerController _controller;

		private bool _onStage;
		private Vector3 _restPosition;
		private Quaternion _restRotation;

		private void Awake()
		{
			if (!_controller) _controller = GetComponentInParent<PlayerController>();
			if (_animatorStates) return;

			var player = GetComponentInParent<PokerPlayer>();
			if (player) _animatorStates = player.GetComponentInChildren<PlayerAnimatorStateController>(true);
		}

		public override void OnNetworkDespawn() => LeaveStage();

		// Idempotent, so a beat may say it on every change without the flag growing a history.
		public void ServerSetSmiling(bool smiling)
		{
			if (!IsServer || !_animatorStates) return;

			_animatorStates.ServerSetBool(_smilingParameter, smiling);
		}

		public void ServerSetDancing(bool dancing)
		{
			if (!IsServer || !_animatorStates) return;

			_animatorStates.ServerSetBool(_dancingParameter, dancing);
		}

		// Local on every screen, read off the replicated victory shot: the body is drawn standing on the table
		// while the root stays in its chair, so nothing the seat or the network owns ever moves.
		public void PlaceOnStage(Transform stage)
		{
			if (!_modelRoot || !stage) return;

			if (!_onStage)
			{
				_restPosition = _modelRoot.localPosition;
				_restRotation = _modelRoot.localRotation;
				_onStage = true;
			}

			_modelRoot.SetPositionAndRotation(stage.position, stage.rotation);
			if (_controller) _controller.SetHeadLookSuspended(true);
		}

		public void LeaveStage()
		{
			if (!_onStage) return;

			_onStage = false;
			_modelRoot.localPosition = _restPosition;
			_modelRoot.localRotation = _restRotation;
			if (_controller) _controller.SetHeadLookSuspended(false);
		}
	}
}
