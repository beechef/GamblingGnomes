using System.Collections.Generic;
using Game.Runtime.GameMode.Poker.Player;
using Localization;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Items
{
	public enum PokerPairLockKind : byte
	{
		Fold = 0,
		Items = 1
	}

	// The user and a player they point at are both barred from something on the streets that follow: folding,
	// or playing items. The price of tying somebody's hands is tying your own.
	[CreateAssetMenu(fileName = "PokerItem_PairLock", menuName = "Game/Poker/Items/Pair Lock")]
	public class PokerItemPairLock : PokerItem
	{
		[Header("Lock")]
		[SerializeField] private PokerPairLockKind _lock;

		[Tooltip("How many streets are locked, starting with the next one. The hand ending lifts it whatever is left.")]
		[MinValue(1)]
		[SerializeField] private int _streets = 1;

		protected override void OnCollectTargetSteps(List<PokerItemTargetKind> steps) => steps.Add(PokerItemTargetKind.Player);

		public override string GetTargetPrompt(PokerItemTargetKind kind) =>
			kind == PokerItemTargetKind.Player ? Localizer.Get(LocalizationKeys.Item.PairLock.Prompt) : base.GetTargetPrompt(kind);

		protected override PokerItemAvailability OnGetAvailability(in PokerItemContext context)
		{
			if (!context.Module.HasNextStreet()) return PokerItemAvailability.Hidden(Localizer.Get(LocalizationKeys.Item.Reason.NoStreetLeft));

			foreach (var player in context.GameMode.SeatedPlayers)
			{
				if (AcceptsPlayer(context, player)) return PokerItemAvailability.Usable;
			}

			return PokerItemAvailability.Dimmed(Localizer.Get(LocalizationKeys.Item.Reason.NobodyInHand));
		}

		public override bool AcceptsPlayer(in PokerItemContext context, PokerPlayer target) =>
			target && target.Data && target != context.User && target.Data.IsInHand;

		protected override void OnUseServer(in PokerItemContext context, in PokerItemUseRequest request)
		{
			var target = context.GameMode.FindSeatedPlayerAtSeat(request.TargetSeat);
			if (!target) return;

			var module = context.Module;
			var street = module.StreetSerial.Value + 1;
			var kind = _lock == PokerPairLockKind.Fold ? PokerItemTableRuleKind.NoFoldSelf : PokerItemTableRuleKind.NoItemsSelf;

			module.ServerAddRule(kind, street, 0, context.User.ClientId, _streets);
			module.ServerAddRule(kind, street, 0, target.ClientId, _streets);
		}
	}
}
