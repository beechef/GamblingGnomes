using System.Threading;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Items
{
	// Halves the user's rate, rounding down, then rolls against what is left.
	[CreateAssetMenu(fileName = "PokerItem_HalfDose", menuName = "Game/Poker/Items/Half Dose")]
	public class PokerItemHalfDose : PokerItemDeathRoll
	{
		protected override PokerItemAvailability OnGetAvailability(in PokerItemContext context)
		{
			if (!context.User || !context.User.Data.IsAlive) return PokerItemAvailability.Dimmed("You are already under.");

			return context.User.Data.HallucinationRate.Value > 0
				? PokerItemAvailability.Usable
				: PokerItemAvailability.Dimmed("You have nothing to halve.");
		}

		protected override Awaitable OnUseServerAsync(PokerItemContext context, PokerItemUseRequest request, CancellationToken ct)
		{
			var data = context.User.Data;
			var before = data.HallucinationRate.Value;

			data.ServerChangeHallucination(before / 2 - before);

			return ServerRollAsync(context, new[] { context.User }, new[] { before }, ct);
		}
	}
}
