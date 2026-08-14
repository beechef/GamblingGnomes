using Game.Runtime.GameMode.Poker.Player;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Abilities
{
	// Sends the holder's neck across the table to put their head over the next player's cards. Both kinds
	// of this card look identical from the outside — the neck goes out either way — and only the cheat one
	// actually reads what it is looking at. That is the whole game: the table sees the act and has to
	// guess at what it was for.
	[CreateAssetMenu(fileName = "Ability_PeekLeft", menuName = "Game/Poker/Abilities/Peek Left")]
	public class PokerAbilityPeekLeft : PokerAbility
	{
		[Header("Peek")]
		[Tooltip("How long the head stays over there. Whatever sight the card grants lasts exactly as long, and not a moment past the lean everyone can see.")]
		[SerializeField] private float _durationSeconds = 5f;

		protected override bool OnActivateServer(PokerGameMode gameMode, PokerPlayer player)
		{
			var peek = player.GetComponentInChildren<PokerPeekController>();
			if (!peek) return false;

			var target = FindNeighbour(gameMode, player);
			if (!target || !target.Rig) return false;

			peek.ServerPeek(target.Rig, _durationSeconds, Kind == PokerAbilityKind.Cheat);

			return true;
		}

		// Seat order runs to the left, the way the action does, so the next seat still holding cards is the
		// player on this one's left. Empty chairs and mucked hands are stepped over rather than spending
		// the card on a peek at nothing.
		private static PokerPlayer FindNeighbour(PokerGameMode gameMode, PokerPlayer player)
		{
			return PokerTableUtility.NextPlayer(gameMode.SeatedPlayers, player.Data.SeatIndex.Value,
				candidate => candidate != player && candidate.Data.CardCount > 0);
		}
	}
}
