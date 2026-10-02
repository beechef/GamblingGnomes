using Game.Runtime.GameMode.Poker.Player;
using Game.Runtime.Player.Camera;
using Sirenix.OdinInspector;
using Unity.Netcode;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Camera
{
	// Holds the hand shot while this player points at one of their own cards for an item, theirs or another
	// player's they are answering. Owner-only; released the moment the pick is over.
	public class PokerCardPickCameraHold : NetworkBehaviour
	{
		[Required]
		[Tooltip("The shot framing the player's own hand (State_OwnCards).")]
		[SerializeField] private PlayerCameraState _state;

		[Tooltip("Where the view goes back to. Empty finds it up the hierarchy.")]
		[SerializeField] private PlayerCameraController _camera;

		[Tooltip("Empty finds it on the player.")]
		[SerializeField] private PokerItemTargetingController _targeting;

		private int _handle;

		public override void OnNetworkSpawn()
		{
			if (!IsOwner) return;

			if (!_camera) _camera = GetComponentInParent<PlayerCameraController>();
			if (!_targeting)
			{
				var player = GetComponentInParent<PokerPlayer>();
				if (player) _targeting = player.ItemTargeting;
			}

			if (_targeting) _targeting.OnOwnCardPickChanged += Refresh;
			Refresh();
		}

		public override void OnNetworkDespawn()
		{
			if (_targeting) _targeting.OnOwnCardPickChanged -= Refresh;
			Release();
		}

		private void Refresh()
		{
			var picking = _targeting && _targeting.IsPickingOwnCard;
			if (picking == (_handle != 0)) return;

			if (picking && _camera && _state) _handle = _camera.Request(_state);
			else Release();
		}

		private void Release()
		{
			if (_handle == 0) return;

			if (_camera) _camera.Release(_handle);
			_handle = 0;
		}
	}
}
