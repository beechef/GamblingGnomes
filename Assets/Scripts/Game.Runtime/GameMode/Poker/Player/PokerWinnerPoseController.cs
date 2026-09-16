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

		private void Awake()
		{
			if (_animatorStates) return;

			var player = GetComponentInParent<PokerPlayer>();
			if (player) _animatorStates = player.GetComponentInChildren<PlayerAnimatorStateController>(true);
		}

		// Idempotent, so a beat may say it on every change without the flag growing a history.
		public void ServerSetSmiling(bool smiling)
		{
			if (!IsServer || !_animatorStates) return;

			_animatorStates.ServerSetBool(_smilingParameter, smiling);
		}
	}
}
