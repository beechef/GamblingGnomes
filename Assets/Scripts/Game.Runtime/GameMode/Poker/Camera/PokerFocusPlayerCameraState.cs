using Game.Runtime.GameMode.Poker.Player;
using Game.Runtime.Player.Camera;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Camera
{
	// Turning to watch whoever the table is watching. It listens for that itself rather than being handed
	// a body: the focus moves several times inside one beat — round the seats as each player eats, down
	// the street as the turn passes — and a state that had to be re-requested for each of those would be
	// torn down and rebuilt in the middle of its own shot.
	public class PokerFocusPlayerCameraState : PlayerCameraLookAtState, IPokerCameraShot
	{
		public PokerCameraShot Shot => PokerCameraShot.FocusPlayer;

		private PokerGameData _bound;

		protected override void OnEnter()
		{
			Bind();

			base.OnEnter();
		}

		protected override void OnExit()
		{
			Unbind();

			base.OnExit();
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

		private void OnDestroy() => Unbind();

		private void HandleFocusChanged(ulong previous, ulong current) => Aim();

		// Null while nobody is being watched, which leaves the head where it is rather than snapping it
		// anywhere: the focus is about to be written again — the next eater, the next player on the clock —
		// and a lurch back to centre between the two would be the only thing anyone noticed.
		protected override Transform ResolveTarget()
		{
			var mode = PokerGameMode.Instance;
			if (!mode || !mode.Data) return null;

			var clientId = mode.Data.FocusClientId.Value;
			if (clientId == PokerGameData.NoTurn) return null;

			var player = PokerPlayer.Find(clientId);
			return player && player.Rig ? player.Rig.FocusPoint : null;
		}
	}
}
