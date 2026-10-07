using Game.Runtime.GameMode.Poker.Player;
using Game.Runtime.Player.Camera;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Camera
{
	// Looking at the cards in this player's own hand — the fan held up in the left hand — rather than the
	// place on the table. Read fresh each aim, since the hold point follows whichever rig is drawn.
	public class PokerOwnHandCameraState : PlayerCameraLookAtState
	{
		[Header("References")]
		[Tooltip("Empty finds it on the player.")]
		[SerializeField] private PokerPlayer _player;

		protected override void OnInitialize()
		{
			if (!_player) _player = GetComponentInParent<PokerPlayer>();
		}

		protected override Transform ResolveTarget() => _player && _player.HandVisual ? _player.HandVisual.HandAnchor : null;
	}
}
