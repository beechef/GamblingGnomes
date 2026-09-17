using System;
using System.Threading;
using Game.Runtime.GameMode.Poker.Player;
using Game.Runtime.Player;
using Game.Runtime.Player.Camera;
using Game.Runtime.Utility;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Camera
{
	// Watching somebody go under, from outside. Everybody cuts to that player's own spectator camera — the
	// one going under as well, which is the whole difference from the eating shot: their death is worth
	// seeing, and it cannot be seen from behind their own eyes. For that one screen the body is drawn in
	// full while the shot lasts, because the owner normally renders only their hands.
	//
	// The head still turns underneath the cut, so the room sees every player looking.
	public class PokerDeathCameraState : PlayerCameraLookAtState
	{
		[Required]
		[Tooltip("Knows who is going under. Normally the one on this same object.")]
		[SerializeField] private PokerDeathCameraBinder _binder;

		[Header("Own Body")]
		[Tooltip("How long after the cut the owner's full body is switched on. The cut is a blend (the brain's default), and a body switched on at once fills the view from inside the head the camera has not left yet.")]
		[MinValue(0f)]
		[SerializeField] private float _showBodyDelay = 0.5f;

		[Tooltip("How long after the shot ends the owner goes back to their hands. The view blends back towards the head, and hands alone switched on at once float in mid air in front of a camera still outside the body.")]
		[MinValue(0f)]
		[SerializeField] private float _hideBodyDelay = 0.5f;

		private PlayerSpectatorCamera _watching;
		private PlayerVisual _revealed;
		private CancellationTokenSource _bodySwitch;

		protected override void OnInitialize()
		{
			if (!_binder) _binder = GetComponent<PokerDeathCameraBinder>();
		}

		protected override void OnExit()
		{
			Watch(null);
			Reveal(null);

			base.OnExit();
		}

		// Put straight back rather than waited on: nothing will be left to finish the wait.
		private void OnDestroy()
		{
			Watch(null);
			CancelBodySwitch();

			if (_revealed) _revealed.SetRenderAllBody(false);
			_revealed = null;
		}

		// A different player went under while this shot was already up.
		public void Refocus() => Aim();

		protected override Transform ResolveTarget()
		{
			var player = _binder ? _binder.Dying : null;
			if (!player || !player.Rig) return null;

			Watch(player.Rig.SpectatorCamera);
			Reveal(player.IsOwner ? player.Visual : null);

			return player.IsOwner ? player.Rig.SelfFocusPoint : player.Rig.FocusPoint;
		}

		private void Watch(PlayerSpectatorCamera spectatorCamera)
		{
			if (_watching == spectatorCamera) return;

			if (_watching) _watching.Release();

			_watching = spectatorCamera;

			if (_watching) _watching.Hold();
		}

		// Put back to the hands on the way out rather than to a remembered value: this only ever reveals
		// the owner's own body, and the owner's resting answer is always the hands. Only one body is ever
		// revealed, so one pending switch is enough — a newer one replaces it.
		private void Reveal(PlayerVisual visual)
		{
			if (_revealed == visual) return;

			if (_revealed) SwitchBodyLater(_revealed, false, _hideBodyDelay);

			_revealed = visual;

			if (_revealed) SwitchBodyLater(_revealed, true, _showBodyDelay);
		}

		private void SwitchBodyLater(PlayerVisual visual, bool renderAllBody, float delay)
		{
			CancelBodySwitch();

			if (delay <= 0f)
			{
				visual.SetRenderAllBody(renderAllBody);
				return;
			}

			_bodySwitch = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
			_ = SwitchBodyAfter(visual, renderAllBody, delay, _bodySwitch.Token);
		}

		private static async Awaitable SwitchBodyAfter(PlayerVisual visual, bool renderAllBody, float delay, CancellationToken ct)
		{
			try
			{
				await AwaitableUtility.WaitUnscaledAsync(delay, ct);
			}
			catch (OperationCanceledException)
			{
				return;
			}

			if (visual) visual.SetRenderAllBody(renderAllBody);
		}

		private void CancelBodySwitch()
		{
			if (_bodySwitch == null) return;

			_bodySwitch.Cancel();
			_bodySwitch.Dispose();
			_bodySwitch = null;
		}
	}
}
