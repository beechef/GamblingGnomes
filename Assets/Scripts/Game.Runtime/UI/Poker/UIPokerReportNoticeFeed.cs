using Game.Runtime.GameMode.Poker;
using Game.Runtime.GameMode.Poker.Abilities;
using Game.Runtime.GameMode.Poker.Modules;
using Game.Runtime.GameMode.Poker.Stages;
using UnityEngine;

namespace Game.Runtime.UI.Poker
{
	// The two moments of an accusation everybody is told about: the name being said, and how it ended.
	// Both ride the same card as every bet and fold, because to the rest of the table they are the same
	// kind of event — somebody did something, and it cost them.
	//
	// The two moments take different shapes. Naming somebody puts a player underneath the verb, drawn like
	// the accuser's own name; the verdict puts blood there. That is the whole reason the notice has more
	// than one.
	public class UIPokerReportNoticeFeed : UIPokerNoticeFeed
	{
		[Header("Wording")]
		[SerializeField] private string _filedAction = "REPORT";

		[SerializeField] private string _caughtAction = "CAUGHT";
		[SerializeField] private string _clearedAction = "INNOCENT";

		[Tooltip("An accusation that never found a face. Nobody was named and nothing was judged.")]
		[SerializeField] private string _droppedDetail = "NOBODY";

		private PokerAbilityModule _module;

		protected override void OnBind()
		{
			_module = GameMode.FindModule<PokerAbilityModule>();
			if (_module == null) return;

			_module.Accusation.OnValueChanged += HandleAccusation;
			_module.ReportAction.OnValueChanged += HandleAnswer;
			_module.LastReport.OnValueChanged += HandleVerdict;
		}

		protected override void OnUnbind()
		{
			if (_module != null)
			{
				_module.LastReport.OnValueChanged -= HandleVerdict;
				_module.ReportAction.OnValueChanged -= HandleAnswer;
				_module.Accusation.OnValueChanged -= HandleAccusation;
			}

			_module = null;
		}

		private void HandleAccusation(PokerReportAccusation previous, PokerReportAccusation current)
		{
			if (current.Sequence == 0) return;

			AnnounceTarget(NameOf(current.AccuserClientId), _filedAction, NameOf(current.TargetClientId));
		}

		// Call, shove, or walking away from a shove: every answer inside the accusation, counted in blood.
		private void HandleAnswer(PokerActionNotice previous, PokerActionNotice current)
		{
			if (current.Sequence == 0) return;

			AnnounceAmount(NameOf(current.ClientId), current.Action.ToString().ToUpperInvariant(), current.Amount);
		}

		// Named from whoever the blood went to, so the card reads as a win rather than as a verdict form:
		// the accuser catching somebody, or the accused walking away with what was thrown at them.
		//
		// It lives exactly as long as the verdict does, read off the stage running it rather than off this
		// feed's own clock — a verdict that fades before the table moves on, or lingers after it has, is
		// the same announcement arriving at the wrong moment.
		private void HandleVerdict(PokerReportResult previous, PokerReportResult current)
		{
			if (current.Sequence == 0) return;

			var lifetime = VerdictDuration();

			if (!current.Called && current.Amount <= 0)
			{
				AnnounceTarget(NameOf(current.AccuserClientId), _filedAction, _droppedDetail, lifetime);
				return;
			}

			var winner = current.WasCheater ? current.AccuserClientId : current.TargetClientId;
			var action = current.WasCheater ? _caughtAction : _clearedAction;

			AnnounceAmount(NameOf(winner), action, current.Amount, lifetime);
		}

		// The stage's own number, the same way every other view here reads its numbers off the stage. With
		// no report stage running the feed falls back to its own default.
		private float VerdictDuration()
		{
			var overlayId = Data.OverlayStageId.Value.ToString();
			if (string.IsNullOrEmpty(overlayId)) return -1f;

			return GameMode.FindStage(overlayId) is PokerReportStage stage ? stage.VerdictDuration : -1f;
		}
	}
}
