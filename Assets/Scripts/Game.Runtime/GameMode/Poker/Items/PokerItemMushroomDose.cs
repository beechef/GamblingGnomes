using System.Collections.Generic;
using Game.Runtime.GameMode.Poker.Player;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Items
{
	// Tampers with a player's next few caps: drawn at random, they either do nothing at all or land twice as
	// hard, Colorful roll included. Which it is, the whole table is told as it is drawn.
	[CreateAssetMenu(fileName = "PokerItem_MushroomDose", menuName = "Game/Poker/Items/Mushroom Dose")]
	public class PokerItemMushroomDose : PokerItem
	{
		private const int Nullified = 0;
		private const int Doubled = 2;

		[Header("Dose")]
		[Tooltip("How many of the target's next caps are tampered with. Kept until eaten, across hands.")]
		[MinValue(1)]
		[SerializeField] private int _caps = 2;

		[Tooltip("Odds the caps do nothing, weighed against the doubling weight.")]
		[MinValue(0)]
		[SerializeField] private int _nullifyWeight = 50;

		[Tooltip("Odds the caps land twice as hard.")]
		[MinValue(0)]
		[SerializeField] private int _doubleWeight = 50;

		[Tooltip("What the table reads when the caps come out empty. {0} is how many.")]
		[SerializeField] private string _nullifiedVerb = "NULLIFIED THE NEXT {0} MUSHROOMS OF";

		[Tooltip("What the table reads when the caps come out doubled. {0} is how many.")]
		[SerializeField] private string _doubledVerb = "DOUBLED THE NEXT {0} MUSHROOMS OF";

		protected override void OnCollectTargetSteps(List<PokerItemTargetKind> steps) => steps.Add(PokerItemTargetKind.Player);

		public override string GetTargetPrompt(PokerItemTargetKind kind) =>
			kind == PokerItemTargetKind.Player ? "POINT AT WHOSE MUSHROOMS YOU SPIKE" : base.GetTargetPrompt(kind);

		public override string GetOutcomeVerb(int outcome) =>
			string.Format(outcome == Nullified ? _nullifiedVerb : _doubledVerb, Mathf.Max(1, _caps));

		protected override PokerItemAvailability OnGetAvailability(in PokerItemContext context)
		{
			foreach (var player in context.GameMode.SeatedPlayers)
			{
				if (AcceptsPlayer(context, player)) return PokerItemAvailability.Usable;
			}

			return PokerItemAvailability.Dimmed("Nobody is left to spike.");
		}

		// Yourself included: doubling or emptying your own next caps is a gamble worth taking.
		public override bool AcceptsPlayer(in PokerItemContext context, PokerPlayer target) =>
			target && target.Data && target.Data.InMatch.Value && target.Data.IsAlive && target.BetItemConsume;

		protected override void OnUseServer(in PokerItemContext context, in PokerItemUseRequest request)
		{
			var target = context.GameMode.FindSeatedPlayerAtSeat(request.TargetSeat);
			if (!target || !target.BetItemConsume) return;

			var dose = DrawDose();
			target.BetItemConsume.ServerQueueDoses(dose, Mathf.Max(1, _caps));

			var notices = context.GameMode.Notices;
			if (notices) notices.ServerAnnounce(PokerNotice.ForItemOutcome(context.User.ClientId, Type, target.ClientId, dose));
		}

		private int DrawDose()
		{
			var nullify = Mathf.Max(0, _nullifyWeight);
			var total = nullify + Mathf.Max(0, _doubleWeight);
			if (total <= 0) return Doubled;

			return Random.Range(0, total) < nullify ? Nullified : Doubled;
		}
	}
}
