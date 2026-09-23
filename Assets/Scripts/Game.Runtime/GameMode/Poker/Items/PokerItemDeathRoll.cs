using System.Collections.Generic;
using System.Threading;
using Game.Runtime.GameMode.Poker.Player;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Items
{
	// An item that rolls against somebody's life, the same roll a Colorful cap sets off: the skull sweeps
	// every bar and stops on the number before anything is paid. Whoever it puts under is out of the hand,
	// counted as a fold once their death has played out, with the turn handed on if it was theirs.
	public abstract class PokerItemDeathRoll : PokerItem
	{
		// Rolls every player given against their own rate, all at once, and waits out the rolls and any death
		// they cause. ratesBefore is each one's rate before the item touched it, so a rung crossed on the way
		// is blinked before the skull sweeps. A player at no rate has nothing to roll against and is skipped.
		protected async Awaitable ServerRollAsync(PokerItemContext context, IReadOnlyList<PokerPlayer> players, IReadOnlyList<int> ratesBefore, CancellationToken ct)
		{
			var rolled = new List<PokerPlayer>();
			var wait = 0f;

			for (var i = 0; i < players.Count; i++)
			{
				var roller = players[i] ? players[i].HallucinationRoll : null;
				if (!roller || !roller.ServerRoll(players[i].Data.HallucinationRate.Value, ratesBefore[i])) continue;

				rolled.Add(players[i]);
				wait = Mathf.Max(wait, roller.ServerOutcomeRemaining);
			}

			if (rolled.Count == 0) return;

			await Awaitable.WaitForSecondsAsync(wait, ct);

			foreach (var player in rolled)
			{
				if (player && player.HallucinationRoll.ServerRollFatal) context.GameMode.ServerFoldOutOfHand(player);
			}
		}
	}
}
