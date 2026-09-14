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
	public class PokerDeathPoseController : NetworkBehaviour
	{
		[Header("Animator")]
		[Tooltip("Bool parameter set while this player is out of the game. A controller without it is skipped, so the pose can be driven before the art lands.")]
		[SerializeField] private string _deadParameter = "IsDead";

		[Header("References")]
		[SerializeField] private PokerPlayerData _data;

		[Required]
		[SerializeField] private PlayerAnimatorStateController _animatorStates;

		public override void OnNetworkSpawn()
		{
			if (!_data) _data = GetComponentInParent<PokerPlayerData>();
			if (!IsServer || !_data) return;

			_data.OnHallucinationChanged += HandleHallucinationChanged;

			Refresh();
		}

		public override void OnNetworkDespawn()
		{
			if (!IsServer || !_data) return;

			_data.OnHallucinationChanged -= HandleHallucinationChanged;
		}

		private void HandleHallucinationChanged(int previous, int current) => Refresh();

		private void Refresh()
		{
			if (!_animatorStates || !_data) return;

			_animatorStates.ServerSetBool(_deadParameter, !_data.IsAlive);
		}
	}
}
