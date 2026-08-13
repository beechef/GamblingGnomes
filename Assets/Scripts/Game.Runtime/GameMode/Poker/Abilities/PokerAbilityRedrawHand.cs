using System.Collections.Generic;
using Game.Runtime.GameMode.Poker.Player;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Abilities
{
	// Throws the hand back and draws fresh — the deck never left the server, so nobody else can tell
	// it happened. Which is the point: this is a cheat, and the report game is how it gets caught.
	[CreateAssetMenu(fileName = "Ability_RedrawHand", menuName = "Game/Poker/Abilities/Redraw Hand")]
	public class PokerAbilityRedrawHand : PokerAbility
	{
		protected override bool OnActivateServer(PokerGameMode gameMode, PokerPlayer player)
		{
			var count = player.Data.CardCount;
			if (count <= 0 || gameMode.Deck.Remaining < count) return false;

			var cards = new List<CardData>(count);
			for (var i = 0; i < count; i++) cards.Add(gameMode.Deck.Draw());

			player.Data.ServerSetHoleCards(cards);
			return true;
		}
	}
}
