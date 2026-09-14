using Game.Runtime.GameMode.Poker.Player;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Camera
{
	// A street where one player answers at a time and the rest watch: on your own turn the view goes down
	// to your own place at the table, because what you are deciding is there; on anybody else's it turns
	// to them, because what is being decided is a person making up their mind.
	//
	// The branch lives here rather than in whatever put this shot up. A state already works out what it is
	// looking at, so "whose turn is it" is one more question about the same target — where a second state
	// swapped in and out on every turn would tear this one down and rebuild it in the middle of its own
	// shot.
	//
	// A viewer needs no special case: it is never their turn, so they watch the player like everybody else.
	public class PokerWagerCameraState : PokerOwnSeatCameraState
	{
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

		private void OnDestroy() => Unbind();

		private void Bind()
		{
			var mode = PokerGameMode.Instance;
			var data = mode ? mode.Data : null;
			if (!data || _bound == data) return;

			Unbind();

			_bound = data;
			_bound.CurrentTurnClientId.OnValueChanged += HandleTurnChanged;
		}

		private void Unbind()
		{
			if (!_bound) return;

			_bound.CurrentTurnClientId.OnValueChanged -= HandleTurnChanged;
			_bound = null;
		}

		private void HandleTurnChanged(ulong previous, ulong current) => Aim();

		protected override Transform ResolveTarget()
		{
			var mode = PokerGameMode.Instance;
			if (!mode || !mode.Data) return null;

			var turn = mode.Data.CurrentTurnClientId.Value;
			if (turn == PokerGameData.NoTurn) return null;

			if (Data && turn == Data.OwnerClientId) return OwnSeatAnchor();

			var player = PokerPlayer.Find(turn);
			return player && player.Rig ? player.Rig.FocusPoint : null;
		}
	}
}
