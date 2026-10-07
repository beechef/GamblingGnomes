using System.Collections.Generic;
using Game.Runtime.GameMode.Poker.Player;
using Localization;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Serialization;

namespace Game.Runtime.GameMode.Poker.Items
{
	// Moves a chosen player's rate up or down by a fixed amount, drawn at random. The whole table is told
	// which way it went. The user may choose themselves.
	[CreateAssetMenu(fileName = "PokerItem_RateShift", menuName = "Game/Poker/Items/Rate Shift")]
	public class PokerItemRateShift : PokerItem
	{
		private const int Lowered = 0;
		private const int Raised = 1;

		[Header("Shift")]
		[Tooltip("How far the rate moves, in rate points (the ceiling is 100).")]
		[MinValue(1)]
		[SerializeField] private int _amount = 20;

		[Tooltip("Odds the rate goes up, weighed against the lowering weight.")]
		[MinValue(0)]
		[SerializeField] private int _raiseWeight = 50;

		[Tooltip("Odds the rate goes down.")]
		[MinValue(0)]
		[SerializeField] private int _lowerWeight = 50;

		[Tooltip("What the table reads when the rate goes up. {0} is the amount.")]
		[LocalizationKey]
		[FormerlySerializedAs("_raisedVerb")]
		[SerializeField] private string _raisedKey = LocalizationKeys.Item.RateShift.Raised;

		[Tooltip("What the table reads when the rate goes down. {0} is the amount.")]
		[LocalizationKey]
		[FormerlySerializedAs("_loweredVerb")]
		[SerializeField] private string _loweredKey = LocalizationKeys.Item.RateShift.Lowered;

		protected override void OnCollectTargetSteps(List<PokerItemTargetKind> steps) => steps.Add(PokerItemTargetKind.Player);

		public override string GetTargetPrompt(PokerItemTargetKind kind) =>
			kind == PokerItemTargetKind.Player ? Localizer.Get(LocalizationKeys.Item.RateShift.Prompt) : base.GetTargetPrompt(kind);

		public override string GetOutcomeVerb(int outcome) => Localizer.Format(outcome == Raised ? _raisedKey : _loweredKey, _amount);

		public override bool AnnouncesOutcome => true;

		protected override PokerItemAvailability OnGetAvailability(in PokerItemContext context)
		{
			foreach (var player in context.GameMode.SeatedPlayers)
			{
				if (AcceptsPlayer(context, player)) return PokerItemAvailability.Usable;
			}

			return PokerItemAvailability.Dimmed(Localizer.Get(LocalizationKeys.Item.Reason.NobodyToShake));
		}

		public override bool AcceptsPlayer(in PokerItemContext context, PokerPlayer target) =>
			target && target.Data && target.Data.InMatch.Value && target.Data.IsAlive;

		protected override void OnUseServer(in PokerItemContext context, in PokerItemUseRequest request)
		{
			var target = context.GameMode.FindSeatedPlayerAtSeat(request.TargetSeat);
			if (!target) return;

			var data = target.Data;
			var raised = DrawRaised();
			var rate = data.HallucinationRate.Value;

			// ponytail: a raise stops one short of the ceiling, since only a roll may put somebody under; roll after the raise if it should kill.
			var next = raised
				? Mathf.Min(rate + _amount, PokerPlayerData.MaxHallucination - 1)
				: Mathf.Max(rate - _amount, 0);
			data.ServerChangeHallucination(next - rate);

			var notices = context.GameMode.Notices;
			if (notices) notices.ServerAnnounce(PokerNotice.ForItemOutcome(context.User.ClientId, Type, target.ClientId, raised ? Raised : Lowered));
		}

		private bool DrawRaised()
		{
			var raise = Mathf.Max(0, _raiseWeight);
			var total = raise + Mathf.Max(0, _lowerWeight);
			return total <= 0 || Random.Range(0, total) < raise;
		}
	}
}
