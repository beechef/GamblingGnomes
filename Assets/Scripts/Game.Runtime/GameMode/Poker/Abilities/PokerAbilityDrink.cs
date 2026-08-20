using System.Collections.Generic;
using Game.Runtime.GameMode.Poker.Player;
using Game.Runtime.Player;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Abilities
{
	// A round of drinks. The holder raises their glass, the table drinks with them, and one of them takes
	// it badly and splutters. Everybody's screen swims for a couple of seconds and the hand carries on
	// underneath it.
	//
	// Both kinds of this card put on exactly the same show, down to who splutters — an act identifiable by
	// what did *not* happen is identifiable, so an honest round that never made anybody choke would mark
	// every choke in the game as a cheat and there would be nothing left to guess at. What the cheat keeps
	// for itself is invisible twice over: the holder stays sober while the table swims, and for as long as
	// it lasts they can read the hand of whoever they just made splutter.
	[CreateAssetMenu(fileName = "Ability_Drink", menuName = "Game/Poker/Abilities/Drink")]
	public class PokerAbilityDrink : PokerAbility
	{
		[Header("Drink")]
		[Tooltip("How long the table swims, and on the cheat kind how long the holder may read the hand they exposed. One number, so the look can never outlast the act that paid for it.")]
		[MinValue(0.1f)]
		[SerializeField] private float _drunkSeconds = 2f;

		// The lockout is the round: the card holds its holder for exactly as long as the table can see
		// them doing it, so there is one number and nothing to keep in step.
		public override float BusySeconds => _drunkSeconds;

		private readonly List<PokerPlayer> _candidates = new();

		protected override bool OnActivateServer(PokerGameMode gameMode, PokerPlayer player)
		{
			var victim = PickVictim(gameMode, player);

			// Nobody left holding cards to choke on it. The card is not spent, which is the same answer the
			// table would read anyway: with one player in the hand there is nothing here to hide.
			if (!victim) return false;

			var cheating = Kind == PokerAbilityKind.Cheat;

			foreach (var seated in gameMode.SeatedPlayers)
			{
				if (!seated || !seated.Data || !seated.Data.IsSeated || !seated.Data.IsAlive) continue;

				seated.ActionAnimator?.ServerPlay(PlayerActionIds.Drink);

				// The holder of the cheat kind raises the glass and does not swallow. Nothing about that is
				// visible from another seat: being drunk is drawn on the drinker's own screen alone.
				if (cheating && seated == player) continue;

				var drink = seated.GetComponentInChildren<PokerDrinkController>();
				if (drink) drink.ServerMakeDrunk(_drunkSeconds);
			}

			// Played after the round so it lands on top of the drinking rather than under it.
			victim.ActionAnimator?.ServerPlay(PlayerActionIds.Spill);

			if (!cheating) return true;

			var holder = player.GetComponentInChildren<PokerDrinkController>();
			if (!holder)
			{
				Debug.LogWarning($"[{nameof(PokerAbilityDrink)}] {player.name} has no {nameof(PokerDrinkController)}, " +
					"so the cheat kind of this card put on the show and granted nothing.");
				return true;
			}

			holder.ServerExpose(victim.Data, _drunkSeconds);

			return true;
		}

		// Anybody at the table but the holder who is still holding cards. Drawn at random rather than taken
		// in seat order: a card that always caught the player on the left would be read off the victim
		// alone, and who spluttered is the one part of this the whole table gets to see.
		private PokerPlayer PickVictim(PokerGameMode gameMode, PokerPlayer player)
		{
			_candidates.Clear();

			foreach (var seated in gameMode.SeatedPlayers)
			{
				if (!seated || seated == player || !seated.Data) continue;
				if (seated.Data.CardCount <= 0) continue;

				_candidates.Add(seated);
			}

			return _candidates.Count == 0 ? null : _candidates[Random.Range(0, _candidates.Count)];
		}
	}
}
