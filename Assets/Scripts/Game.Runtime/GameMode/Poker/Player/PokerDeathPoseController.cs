using DG.Tweening;
using Game.Runtime.GameMode.Poker.Hallucination;
using Game.Runtime.Player;
using Sirenix.OdinInspector;
using Unity.Netcode;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Player
{
	// Going under is a pose the whole table watches, and whether a player has gone under is already on
	// the wire — so it is derived rather than toggled, the same shape PokerHandPoseController takes for
	// holding cards. The animator's own death layer does the rest: the clip plays once and holds its last
	// frame for as long as the flag stands, and the flag drops the moment the rate does.
	//
	// The head stops following the look at once, on every machine: the player who went under can still turn
	// their view, but a corpse turning its head after the mouse is the one thing the death clip must not have
	// fighting it, and every machine writes the replicated look onto its own copy of that head. The pose
	// itself waits until the blink the ceiling sets off has opened again, or the fall plays behind a shut eye,
	// and then a beat more, so the camera cutting to the body arrives before the fall does rather than
	// halfway through it.
	public class PokerDeathPoseController : NetworkBehaviour
	{
		[Header("Animator")]
		[Tooltip("Bool parameter set while this player is out of the game. A controller without it is skipped, so the pose can be driven before the art lands.")]
		[SerializeField] private string _deadParameter = "IsDead";

		[Tooltip("Seconds between going under and the death pose starting — the beat the death shot uses to arrive. Everything timed against the clip (the head vanishing) counts from after this.")]
		[MinValue(0f)]
		[SerializeField] private float _poseDelay = 0.4f;

		[Header("References")]
		[SerializeField] private PokerPlayerData _data;

		[Required]
		[SerializeField] private PlayerAnimatorStateController _animatorStates;

		[Tooltip("Told to stop turning the head with the look while this player is out of the game. Empty resolves from the parents.")]
		[SerializeField] private PlayerController _playerController;

		[Tooltip("Whose blink the death waits out. Empty resolves from the player.")]
		[SerializeField] private PokerHallucinationController _hallucination;

		private Tween _pendingPose;

		// When the death beat starts after going under: once the blink the crossing sets off has opened again.
		// Every machine answers the same from the replicated rates, so the shot and the pose agree on screens
		// that never blink.
		public float BlinkWait(int previous, int current) =>
			_hallucination && _hallucination.CrossesRung(previous, current) ? _hallucination.TransitionDuration : 0f;

		public float PoseStartDelay(int previous, int current) => BlinkWait(previous, current) + _poseDelay;

		public override void OnNetworkSpawn()
		{
			if (!_data) _data = GetComponentInParent<PokerPlayerData>();
			if (!_playerController) _playerController = GetComponentInParent<PlayerController>();
			if (!_hallucination)
			{
				var player = GetComponentInParent<PokerPlayer>();
				if (player) _hallucination = player.GetComponentInChildren<PokerHallucinationController>(true);
			}
			if (!_data) return;

			_data.OnHallucinationChanged += HandleHallucinationChanged;

			// Late join: somebody already under arrives already lying still, with no fall to wait for.
			ApplyControl();
			if (IsServer) SetPose(!_data.IsAlive);
		}

		public override void OnNetworkDespawn()
		{
			if (_data) _data.OnHallucinationChanged -= HandleHallucinationChanged;

			_pendingPose?.Kill();
			_pendingPose = null;

			if (_playerController) _playerController.SetHeadLookSuspended(false);
		}

		private void HandleHallucinationChanged(int previous, int current)
		{
			ApplyControl();

			if (!IsServer || !_data) return;

			_pendingPose?.Kill();
			_pendingPose = null;

			var delay = PoseStartDelay(previous, current);

			if (_data.IsAlive || delay <= 0f)
			{
				SetPose(!_data.IsAlive);
				return;
			}

			_pendingPose = DOVirtual.DelayedCall(delay, () =>
				{
					_pendingPose = null;
					SetPose(_data && !_data.IsAlive);
				})
				.SetLink(gameObject);
		}

		private void ApplyControl()
		{
			if (_playerController && _data) _playerController.SetHeadLookSuspended(!_data.IsAlive);
		}

		private void SetPose(bool dead)
		{
			if (_animatorStates) _animatorStates.ServerSetBool(_deadParameter, dead);
		}
	}
}
