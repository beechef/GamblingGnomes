using Game.Runtime.GameMode.Poker.Abilities;
using Game.Runtime.GameMode.Poker.Modules;
using UnityEngine;

namespace Game.Runtime.UI.Poker
{
	// The two moments of an accusation everybody is told about: the name being said, and how it ended.
	// Both ride the same card as every bet and fold, because to the rest of the table they are the same
	// kind of event — somebody did something, and it cost them.
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
			_module.LastReport.OnValueChanged += HandleVerdict;
		}

		protected override void OnUnbind()
		{
			if (_module != null)
			{
				_module.LastReport.OnValueChanged -= HandleVerdict;
				_module.Accusation.OnValueChanged -= HandleAccusation;
			}

			_module = null;
		}

		private void HandleAccusation(PokerReportAccusation previous, PokerReportAccusation current)
		{
			if (current.Sequence == 0) return;

			Announce(NameOf(current.AccuserClientId), _filedAction, NameOf(current.TargetClientId));
		}

		// Named from whoever the blood went to, so the card reads as a win rather than as a verdict form:
		// the accuser catching somebody, or the accused walking away with what was thrown at them.
		private void HandleVerdict(PokerReportResult previous, PokerReportResult current)
		{
			if (current.Sequence == 0) return;

			if (!current.Called)
			{
				Announce(NameOf(current.AccuserClientId), _filedAction, _droppedDetail);
				return;
			}

			var winner = current.WasCheater ? current.AccuserClientId : current.TargetClientId;
			var action = current.WasCheater ? _caughtAction : _clearedAction;

			Announce(NameOf(winner), action, current.Amount > 0 ? $"+{current.Amount}" : null);
		}
	}
}
