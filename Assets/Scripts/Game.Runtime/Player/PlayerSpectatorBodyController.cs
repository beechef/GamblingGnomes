using System;
using System.Threading;
using Game.Runtime.Player.Camera;
using Game.Runtime.Utility;
using Sirenix.OdinInspector;
using Unity.Netcode;
using UnityEngine;

namespace Game.Runtime.Player
{
	// The owner draws only their own hands, which is right from behind their own eyes and wrong from
	// anywhere else: a shot from outside — any spectator camera, their own or somebody else's — can have
	// the owner's body in it, and hands floating in a chair read as a broken rig. So while this client looks
	// through a spectator camera the owner's full body is drawn, and the hands come back once the view is
	// home. The one writer of PlayerVisual.RenderAllBody.
	//
	// Both switches wait a beat, because a cut is a blend (the brain's default): switched on at once the
	// body fills the view from inside a head the camera has not left yet, and switched back at once the
	// hands float in front of a camera still outside the body.
	public class PlayerSpectatorBodyController : NetworkBehaviour
	{
		[Required]
		[SerializeField] private PlayerVisual _visual;

		[MinValue(0f)]
		[SerializeField] private float _showBodyDelay = 0.5f;

		[MinValue(0f)]
		[SerializeField] private float _hideBodyDelay = 0.5f;

		private CancellationTokenSource _switch;
		private bool _bound;

		public override void OnNetworkSpawn()
		{
			if (!_visual) _visual = GetComponentInParent<PlayerVisual>();

			Bind();
		}

		public override void OnNetworkDespawn() => Unbind();

		// OnEnable covers a switch off and on after spawn, which OnNetworkSpawn does not; ownership is only
		// settled once spawned.
		private void OnEnable()
		{
			if (IsSpawned) Bind();
		}

		private void OnDisable() => Unbind();

		private void Bind()
		{
			if (_bound || !IsOwner) return;

			_bound = true;
			PlayerSpectatorCamera.OnAnyLiveChanged += HandleSpectatorChanged;

			if (PlayerSpectatorCamera.AnyLive) Switch(true, 0f);
		}

		private void Unbind()
		{
			if (!_bound) return;

			_bound = false;
			PlayerSpectatorCamera.OnAnyLiveChanged -= HandleSpectatorChanged;

			CancelSwitch();
			if (_visual) _visual.SetRenderAllBody(false);
		}

		private void HandleSpectatorChanged(bool live) => Switch(live, live ? _showBodyDelay : _hideBodyDelay);

		// One pending switch at a time: a newer one replaces it, so a cut back before the body came on never
		// turns it on late.
		private void Switch(bool renderAllBody, float delay)
		{
			CancelSwitch();
			if (!_visual) return;

			if (delay <= 0f)
			{
				_visual.SetRenderAllBody(renderAllBody);
				return;
			}

			_switch = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
			_ = SwitchAfter(renderAllBody, delay, _switch.Token);
		}

		private async Awaitable SwitchAfter(bool renderAllBody, float delay, CancellationToken ct)
		{
			try
			{
				await AwaitableUtility.WaitUnscaledAsync(delay, ct);
			}
			catch (OperationCanceledException)
			{
				return;
			}

			if (_visual) _visual.SetRenderAllBody(renderAllBody);
		}

		private void CancelSwitch()
		{
			if (_switch == null) return;

			_switch.Cancel();
			_switch.Dispose();
			_switch = null;
		}
	}
}
