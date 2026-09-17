using Game.Runtime.GameMode.Poker.Player;
using Game.Runtime.Player.Camera;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Camera
{
	// Turning to watch whoever the table is watching. It listens for that itself rather than being handed
	// a body: the focus moves several times inside one beat — round the seats as each player swallows a
	// cap — and a state that had to be re-requested for each of those would be torn down and rebuilt in
	// the middle of its own shot.
	//
	// Which point on that body it aims at depends on whether the body is yours. Everyone else offers a
	// face; you offer the spot in front of your own chest, because aiming your own eye at your own face is
	// aiming it at something a hand's breadth away and the head comes out wrenched round. The rig owns
	// both points, so this only has to ask which one it wants.
	//
	// Watching somebody else, the view also cuts to that player's own spectator camera, which frames them
	// far better than a head turned across the table can. The head still turns underneath it, so the rest
	// of the room sees this player looking. The eater is never cut away from their own eyes.
	public class PokerFocusPlayerCameraState : PlayerCameraLookAtState
	{
		private PokerGameData _bound;
		private PlayerSpectatorCamera _watching;

		protected override void OnEnter()
		{
			Bind();

			base.OnEnter();
		}

		protected override void OnExit()
		{
			Unbind();
			Watch(null);

			base.OnExit();
		}

		private void OnDestroy()
		{
			Unbind();
			Watch(null);
		}

		private void Watch(PlayerSpectatorCamera spectatorCamera)
		{
			if (_watching == spectatorCamera) return;

			if (_watching) _watching.Release();

			_watching = spectatorCamera;

			if (_watching) _watching.Hold();
		}

		private void Bind()
		{
			var mode = PokerGameMode.Instance;
			var data = mode ? mode.Data : null;
			if (!data || _bound == data) return;

			Unbind();

			_bound = data;
			_bound.FocusClientId.OnValueChanged += HandleFocusChanged;
		}

		private void Unbind()
		{
			if (!_bound) return;

			_bound.FocusClientId.OnValueChanged -= HandleFocusChanged;
			_bound = null;
		}

		private void HandleFocusChanged(ulong previous, ulong current) => Aim();

		// Null while nobody is being watched, which leaves the head where it is rather than snapping it
		// anywhere: the focus is about to be written again — the next eater — and a lurch back to centre
		// between the two would be the only thing anyone noticed.
		protected override Transform ResolveTarget()
		{
			var mode = PokerGameMode.Instance;
			if (!mode || !mode.Data) return null;

			var clientId = mode.Data.FocusClientId.Value;
			if (clientId == PokerGameData.NoTurn) return null;

			var player = PokerPlayer.Find(clientId);
			if (!player || !player.Rig) return null;

			// Resolved here because this is where the answer lands, retries included: the eater's body can
			// arrive a few frames after the focus does. A focus cleared between two eaters keeps the cut
			// where it is, for the same reason the head does.
			Watch(player.IsOwner ? null : player.Rig.SpectatorCamera);

			return player.IsOwner ? player.Rig.SelfFocusPoint : player.Rig.FocusPoint;
		}
	}
}
