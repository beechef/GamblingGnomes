using System;
using System.Collections.Generic;
using System.Threading;
using Game.Runtime.GameMode.Poker.Player;
using Game.Runtime.Player;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Abilities
{
	// A shuffle the whole table watches, and — on the cheat half of the card only — a hand quietly swapped
	// underneath it. The deck never left the server, so the act is the only evidence there is, which is
	// exactly why both halves play the same one: the honest card is the shuffle and nothing else, and a
	// player who draws it looks no different from a player who cheated.
	[CreateAssetMenu(fileName = "Ability_RedrawHand", menuName = "Game/Poker/Abilities/Redraw Hand")]
	public class PokerAbilityRedrawHand : PokerAbility
	{
		[Header("Redraw")]
		[Tooltip("Seconds between the shuffle starting and the hand changing. Long enough for the hands to come back down on the table: cards that swap while they are still in the air swap where nobody is looking, and being looked at is the whole point of the act.")]
		[MinValue(0f)]
		[SerializeField] private float _redrawDelaySeconds = 1.2f;

		protected override bool OnActivateServer(PokerGameMode gameMode, PokerPlayer player)
		{
			player.ActionAnimator?.ServerPlay(PlayerActionIds.ShuffleCards);

			// Spent either way. An honest draw that refused to be played would be a card the table could
			// identify by what did *not* happen, and the guessing game only works while the two are
			// indistinguishable from outside.
			if (Kind != PokerAbilityKind.Cheat) return true;

			var count = player.Data.CardCount;

			// Nothing to swap, or a deck too thin to do it with. The shuffle still happened and the card is
			// still spent — falling back to exactly what the honest card does, rather than fizzling in a way
			// that would only ever be visible on a cheat.
			if (count <= 0 || gameMode.Deck.Remaining < count) return true;

			_ = RedrawAfterDelay(gameMode, player, _redrawDelaySeconds, gameMode.destroyCancellationToken);
			return true;
		}

		// Left running rather than awaited: the act has already happened and been announced, and nothing is
		// waiting on the swap. Awaitable rather than async void, so an exception has somewhere to go, and it
		// carries the mode's own lifetime — a table torn down mid-shuffle takes the pending redraw with it
		// instead of writing into a corpse. Nothing to undo on cancel either: the cards are drawn at the end
		// of the wait, never at the start of it.
		private static async Awaitable RedrawAfterDelay(PokerGameMode gameMode, PokerPlayer player, float delay,
			CancellationToken ct)
		{
			try
			{
				if (delay > 0f) await Awaitable.WaitForSecondsAsync(delay, ct);
			}
			catch (OperationCanceledException)
			{
				return;
			}

			// The hand can have been swept, folded or dealt again while the hands were coming back down, and
			// the player can have left the table altogether.
			if (ct.IsCancellationRequested || !gameMode || !player || !player.Data) return;

			var count = player.Data.CardCount;
			if (count <= 0 || gameMode.Deck.Remaining < count) return;

			var cards = new List<CardData>(count);
			for (var i = 0; i < count; i++) cards.Add(gameMode.Deck.Draw());

			player.Data.ServerReplaceHoleCards(cards);
		}
	}
}
