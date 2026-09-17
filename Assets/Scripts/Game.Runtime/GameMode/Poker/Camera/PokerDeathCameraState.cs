using Game.Runtime.GameMode.Poker.Player;
using Game.Runtime.Player.Camera;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Camera
{
	// Watching somebody go under, from outside. Everybody cuts to that player's own spectator camera — the
	// one going under as well, which is the whole difference from the eating shot: their death is worth
	// seeing, and it cannot be seen from behind their own eyes. The owner's full body being drawn for that
	// is not this state's business: PlayerSpectatorBodyController does it for every spectator shot.
	//
	// The head still turns underneath the cut, so the room sees every player looking.
	public class PokerDeathCameraState : PlayerCameraLookAtState
	{
		[Required]
		[Tooltip("Knows who is going under. Normally the one on this same object.")]
		[SerializeField] private PokerDeathCameraBinder _binder;

		private PlayerSpectatorCamera _watching;

		protected override void OnInitialize()
		{
			if (!_binder) _binder = GetComponent<PokerDeathCameraBinder>();
		}

		protected override void OnExit()
		{
			Watch(null);

			base.OnExit();
		}

		private void OnDestroy() => Watch(null);

		// A different player went under while this shot was already up.
		public void Refocus() => Aim();

		protected override Transform ResolveTarget()
		{
			var player = _binder ? _binder.Dying : null;
			if (!player || !player.Rig) return null;

			Watch(player.Rig.SpectatorCamera);

			return player.IsOwner ? player.Rig.SelfFocusPoint : player.Rig.FocusPoint;
		}

		private void Watch(PlayerSpectatorCamera spectatorCamera)
		{
			if (_watching == spectatorCamera) return;

			if (_watching) _watching.Release();

			_watching = spectatorCamera;

			if (_watching) _watching.Hold();
		}
	}
}
