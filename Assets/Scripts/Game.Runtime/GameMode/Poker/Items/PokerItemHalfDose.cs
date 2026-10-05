using System;
using System.Collections.Generic;
using System.Threading;
using Game.Runtime.GameMode.Poker.Player;
using Localization;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Items
{
	// Halves a chosen player's rate, rounding down, then they roll against what is left. The user may choose
	// themselves.
	[CreateAssetMenu(fileName = "PokerItem_HalfDose", menuName = "Game/Poker/Items/Half Dose")]
	public class PokerItemHalfDose : PokerItemDeathRoll
	{
		protected override void OnCollectTargetSteps(List<PokerItemTargetKind> steps) => steps.Add(PokerItemTargetKind.Player);

		public override string GetTargetPrompt(PokerItemTargetKind kind) =>
			kind == PokerItemTargetKind.Player ? Localizer.Get(LocalizationKeys.Item.HalfDose.Prompt) : base.GetTargetPrompt(kind);

		protected override PokerItemAvailability OnGetAvailability(in PokerItemContext context)
		{
			foreach (var player in context.GameMode.SeatedPlayers)
			{
				if (AcceptsPlayer(context, player)) return PokerItemAvailability.Usable;
			}

			return PokerItemAvailability.Dimmed(Localizer.Get(LocalizationKeys.Item.Reason.NobodyToHalve));
		}

		public override bool AcceptsPlayer(in PokerItemContext context, PokerPlayer target) =>
			target && target.Data && target.Data.InMatch.Value && target.Data.IsAlive && target.Data.HallucinationRate.Value > 0;

		protected override Awaitable OnUseServerAsync(PokerItemContext context, PokerItemUseRequest request, CancellationToken ct)
		{
			var target = context.GameMode.FindSeatedPlayerAtSeat(request.TargetSeat);
			if (!target) return ServerRollAsync(context, Array.Empty<PokerPlayer>(), Array.Empty<int>(), ct);

			var data = target.Data;
			var before = data.HallucinationRate.Value;
			data.ServerChangeHallucination(before / 2 - before);
			return ServerRollAsync(context, new[] { target }, new[] { before }, ct);
		}
	}
}
