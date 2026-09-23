using System.Collections.Generic;
using System.Threading;
using Game.Runtime.GameMode.Poker.Player;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Items
{
	// An item that rolls against somebody's life, the same roll a Colorful cap sets off: the skull sweeps
	// every bar and stops on the number before anything is paid. Whoever it puts under is out of the hand,
	// counted as a fold once their death has played out, with the turn handed on if it was theirs.
	public abstract class PokerItemDeathRoll : PokerItem
	{
		[Header("Roll")]
		[Tooltip("Seconds before the skull starts to sweep. Stretched to cover a blink when the rate crossed a rung.")]
		[MinValue(0f)]
		[SerializeField] private float _rollLeadIn = 0.6f;

		[MinValue(0.1f)]
		[SerializeField] private float _rollSweep = 2.5f;

		[Tooltip("Seconds the skull rests on the number before a fatal roll puts them under.")]
		[MinValue(0f)]
		[SerializeField] private float _rollHold = 1f;

		// Rolls every player given against their own rate, all at once, and waits out the rolls and any death
		// they cause. ratesBefore is each one's rate before the item touched it, so a rung crossed on the way
		// is blinked before the skull sweeps. A player at no rate has nothing to roll against and is skipped.
		protected async Awaitable ServerRollAsync(PokerItemContext context, IReadOnlyList<PokerPlayer> players, IReadOnlyList<int> ratesBefore, CancellationToken ct)
		{
			var rolled = new List<(PokerPlayer Player, int Rate)>();
			var wait = 0f;

			for (var i = 0; i < players.Count; i++)
			{
				var player = players[i];
				if (!player || !player.Data || !player.HallucinationRoll || !player.Data.IsAlive) continue;

				var rate = player.Data.HallucinationRate.Value;
				if (rate <= 0) continue;

				var roll = Random.Range(0, PokerPlayerData.MaxHallucination);

				player.HallucinationRoll.ServerQueueRoll(roll, roll < rate, ratesBefore[i], rate);
				player.HallucinationRoll.ServerStartQueuedRoll(_rollLeadIn, _rollSweep, _rollHold);

				rolled.Add((player, rate));
				wait = Mathf.Max(wait, player.HallucinationRoll.ServerRollRemaining);
			}

			if (rolled.Count == 0) return;

			await Awaitable.WaitForSecondsAsync(wait, ct);

			var dying = new List<PokerPlayer>();
			var deathWait = 0f;

			foreach (var (player, rate) in rolled)
			{
				if (!player || !player.HallucinationRoll.ServerRollFatal) continue;

				dying.Add(player);

				var pose = player.GetComponentInChildren<PokerDeathPoseController>(true);
				if (pose) deathWait = Mathf.Max(deathWait, pose.DeathWait(rate, PokerPlayerData.MaxHallucination));
			}

			if (dying.Count == 0) return;

			if (deathWait > 0f) await Awaitable.WaitForSecondsAsync(deathWait, ct);

			foreach (var player in dying) context.GameMode.ServerFoldOutOfHand(player);
		}
	}
}
