using Game.Runtime.GameMode.Poker.Player;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Abilities
{
	// Puts the holder's glasses on. Both kinds of this card look identical from the outside — the gesture
	// plays and the prop shows either way — and only the cheat one actually reads anything through them:
	// the next few face-down cards of the board, for as long as the glasses stay on. Same game as the
	// neck: the table sees the act and has to guess what it was for.
	//
	// The card talks to the peek controller, which owns everything the act means here; the glasses
	// underneath stay cosmetic, a player piece any mode can wear.
	[CreateAssetMenu(fileName = "Ability_Glasses", menuName = "Game/Poker/Abilities/Peek Board")]
	public class PokerAbilityPeekBoard : PokerAbility
	{
		[Header("Peek")]
		[Tooltip("How many face-down board cards the cheat kind shows, counted past what the table has been shown. The normal kind shows none whatever this says.")]
		[SerializeField] private int _revealCount = 2;

		[Tooltip("How long the glasses stay on. Whatever sight the card grants lasts exactly as long, and not a moment past the wear everyone can see.")]
		[SerializeField] private float _durationSeconds = 5f;

		// The lockout is the lean: the card holds the player for exactly as long as the table can see
		// them doing it, so there is one number and nothing to keep in step.
		public override float BusySeconds => _durationSeconds;

		protected override bool OnActivateServer(PokerGameMode gameMode, PokerPlayer player)
		{
			var peek = player.GetComponentInChildren<PokerBoardPeekController>();
			if (!peek) return false;

			// A board with nothing left face down gives the glasses nothing to read — and nothing to bluff
			// about either, so the card fizzles rather than being spent on an empty look.
			var data = gameMode.Data;
			if (data.CommunityCards.Count <= data.RevealedCommunityCards.Value) return false;

			peek.ServerPeek(_durationSeconds, Kind == PokerAbilityKind.Cheat ? Mathf.Max(1, _revealCount) : 0);

			return true;
		}
	}
}
