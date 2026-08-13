using Game.Runtime.GameMode.Poker.Player;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Abilities
{
	// Buys the clock back. Only means anything while it is actually your turn and a clock is running,
	// so it refuses anywhere else rather than burning the card on nothing.
	[CreateAssetMenu(fileName = "Ability_ExtraTime", menuName = "Game/Poker/Abilities/Extra Time")]
	public class PokerAbilityExtraTime : PokerAbility
	{
		[Header("Effect")]
		[SerializeField] private float _bonusSeconds = 15f;

		protected override bool OnActivateServer(PokerGameMode gameMode, PokerPlayer player)
		{
			var data = gameMode.Data;

			if (data.CurrentTurnClientId.Value != player.ClientId) return false;
			if (data.TurnDuration.Value <= 0f) return false;

			gameMode.BeginTurn(player.ClientId, data.TurnRemaining + Mathf.Max(0f, _bonusSeconds));
			return true;
		}
	}
}
