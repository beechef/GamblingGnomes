using DG.Tweening;
using Game.Runtime.GameMode.Poker.Player;
using Game.Runtime.Player.Camera;
using Sirenix.OdinInspector;
using Unity.Netcode;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Camera
{
	// Holds the hand shot while this player points at one of their own cards for an item, theirs or another
	// player's they are answering, and zooms the lens in on it. Owner-only; released the moment the pick is over.
	public class PokerCardPickCameraHold : NetworkBehaviour
	{
		[Required]
		[Tooltip("The shot framing the cards in the player's hand (State_CardPick).")]
		[SerializeField] private PlayerCameraState _state;

		[Tooltip("Where the view goes back to. Empty finds it up the hierarchy.")]
		[SerializeField] private PlayerCameraController _camera;

		[Tooltip("Empty finds it on the player.")]
		[SerializeField] private PokerItemTargetingController _targeting;

		[Header("Zoom")]
		[Tooltip("Empty finds it on the player.")]
		[SerializeField] private PlayerCameraFovController _fov;

		[Tooltip("Field of view while picking. Lower is closer.")]
		[Range(10f, 90f)]
		[SerializeField] private float _zoomFieldOfView = 38f;

		[MinValue(0f)]
		[SerializeField] private float _zoomDuration = 0.35f;

		[SerializeField] private Ease _zoomEase = Ease.OutCubic;

		private int _handle;
		private PlayerCameraFovModifier _zoom;
		private Tween _zoomTween;

		public override void OnNetworkSpawn()
		{
			if (!IsOwner) return;

			if (!_camera) _camera = GetComponentInParent<PlayerCameraController>();
			var player = GetComponentInParent<PokerPlayer>();
			if (!_targeting && player) _targeting = player.ItemTargeting;
			if (!_fov && player) _fov = player.GetComponentInChildren<PlayerCameraFovController>(true);

			if (_targeting) _targeting.OnOwnCardPickChanged += Refresh;
			Refresh();
		}

		public override void OnNetworkDespawn()
		{
			if (_targeting) _targeting.OnOwnCardPickChanged -= Refresh;
			Release();

			_zoomTween?.Kill();
			if (_fov && _zoom != null) _fov.Remove(_zoom);
			_zoom = null;
		}

		private void Refresh()
		{
			var picking = _targeting && _targeting.IsPickingOwnCard;
			if (picking == (_handle != 0)) return;

			if (picking && _camera && _state) _handle = _camera.Request(_state);
			else Release();

			Zoom(picking);
		}

		private void Release()
		{
			if (_handle == 0) return;

			if (_camera) _camera.Release(_handle);
			_handle = 0;
		}

		private void Zoom(bool zoomed)
		{
			if (!_fov) return;

			if (_zoom == null)
			{
				if (!zoomed) return;
				_zoom = _fov.Add(_zoomFieldOfView);
				_zoom.Weight = 0f;
			}

			var zoom = _zoom;
			_zoomTween?.Kill();
			_zoomTween = DOTween.To(() => zoom.Weight, weight => zoom.Weight = weight, zoomed ? 1f : 0f, _zoomDuration)
				.SetEase(_zoomEase)
				.SetLink(gameObject)
				.OnComplete(() =>
				{
					if (zoomed || _zoom != zoom) return;

					_fov.Remove(zoom);
					_zoom = null;
				});
		}
	}
}
