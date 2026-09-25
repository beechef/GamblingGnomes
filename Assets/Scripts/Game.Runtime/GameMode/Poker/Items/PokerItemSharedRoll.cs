using System.Collections.Generic;
using System.Threading;
using Game.Runtime.GameMode.Poker.Player;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Items
{
	// The user and a player they choose each roll against their own rate, at the same moment.
	[CreateAssetMenu(fileName = "PokerItem_SharedRoll", menuName = "Game/Poker/Items/Shared Roll")]
	public class PokerItemSharedRoll : PokerItemDeathRoll
	{
		protected override void OnCollectTargetSteps(List<PokerItemTargetKind> steps) => steps.Add(PokerItemTargetKind.Player);

		public override string GetTargetPrompt(PokerItemTargetKind kind) =>
			kind == PokerItemTargetKind.Player ? "POINT AT WHO ROLLS WITH YOU" : base.GetTargetPrompt(kind);

		protected override PokerItemAvailability OnGetAvailability(in PokerItemContext context)
		{
			if (!context.User || !context.User.Data.IsAlive) return PokerItemAvailability.Dimmed("You are already under.");

			foreach (var player in context.GameMode.SeatedPlayers)
			{
				if (AcceptsPlayer(context, player)) return PokerItemAvailability.Usable;
			}

			return PokerItemAvailability.Dimmed("Nobody else is left to roll.");
		}

		public override bool AcceptsPlayer(in PokerItemContext context, PokerPlayer target) =>
			target && target.Data && target != context.User && target.Data.InMatch.Value && target.Data.IsAlive;

		protected override Awaitable OnUseServerAsync(PokerItemContext context, PokerItemUseRequest request, CancellationToken ct)
		{
			var user = context.User;
			var target = context.GameMode.FindSeatedPlayerAtSeat(request.TargetSeat);

			return ServerRollAsync(context,
				new[] { user, target },
				new[] { user.Data.HallucinationRate.Value, target ? target.Data.HallucinationRate.Value : 0 },
				ct);
		}
	}
}
