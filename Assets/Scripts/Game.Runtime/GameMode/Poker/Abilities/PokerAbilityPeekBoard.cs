using Game.Runtime.GameMode.Poker.Player;
using Game.Runtime.Player;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Abilities
{
	// Puts the holder's glasses on. Both kinds of this card look identical from the outside — the gesture
	// plays and the prop shows either way — and only the cheat one actually reads anything through them:
	// the next few face-down cards of the board, for as long as the glasses stay on. Same game as the
	// neck: the table sees the act and has to guess what it was for.
	//
	// Everything the card touches lives on the glasses component, not on the table's player data — the
	// glasses are a player piece any mode can wear, and poker only installs its own reading of them.
	[CreateAssetMenu(fileName = "Ability_Glasses", menuName = "Game/Poker/Abilities/Peek Board")]
	public class PokerAbilityPeekBoard : PokerAbility
	{
		[Header("Peek")]
		[Tooltip("How many face-down board cards the cheat kind shows, counted past what the table has been shown. The normal kind shows none whatever this says.")]
		[SerializeField] private int _revealCount = 2;

		[Tooltip("How long the glasses stay on. Whatever sight the card grants lasts exactly as long, and not a moment past the wear everyone can see.")]
		[SerializeField] private float _durationSeconds = 5f;

		protected override bool OnActivateServer(PokerGameMode gameMode, PokerPlayer player)
		{
			var glasses = player.GetComponentInChildren<PlayerGlassesController>();
			if (!glasses) return false;

			// A board with nothing left face down gives the glasses nothing to read — and nothing to bluff
			// about either, so the card fizzles rather than being spent on an empty look.
			var data = gameMode.Data;
			if (data.CommunityCards.Count <= data.RevealedCommunityCards.Value) return false;

			glasses.ServerWear(_durationSeconds, Kind == PokerAbilityKind.Cheat ? Mathf.Max(1, _revealCount) : 0);

			return true;
		}
	}
}
