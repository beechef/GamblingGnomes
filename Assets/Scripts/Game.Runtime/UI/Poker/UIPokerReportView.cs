using Game.Runtime.GameMode.Poker.Abilities;
using Game.Runtime.GameMode.Poker.Modules;
using Game.Runtime.GameMode.Poker.Player;
using Game.Runtime.GameMode.Poker.Stages;
using Game.Runtime.UI.Progress;
using TMPro;
using Unity.Collections;
using UnityEngine;

namespace Game.Runtime.UI.Poker
{
	// The report played out on screen: who is pointing at whom, what the accused has put up to be believed,
	// then the verdict when it falls. Up only while the report overlay runs — the stage owns the pacing and
	// the duel's two clocks, this panel just mirrors them. What the two of them actually press lives on the
	// action bar, which is where this table asks for a number either way.
	public class UIPokerReportView : UIPokerView
	{
		[Header("Panel")]
		[SerializeField] private GameObject _panel;

		[Header("Accusation")]
		[SerializeField] private TextMeshProUGUI _accusationLabel;

		[Tooltip("Counts down whichever clock the report is running: each side's turn while they are being asked, then the verdict.")]
		[SerializeField] private UITimerBar _timerBar;

		[Header("Verdict")]
		[SerializeField] private GameObject _verdictRoot;
		[SerializeField] private TextMeshProUGUI _verdictLabel;

		private PokerAbilityModule _module;

		protected override bool WantsTick => _panel && _panel.activeSelf && Data && (Data.HasTurn || Data.HasStageTimer);

		private void Awake()
		{
			if (_panel) _panel.SetActive(false);
		}

		protected override void OnBind()
		{
			_module = GameMode.FindModule<PokerAbilityModule>();
			if (_module == null) return;

			Data.OverlayStageId.OnValueChanged += HandleOverlayChanged;
			Data.CurrentTurnClientId.OnValueChanged += HandleTurnChanged;
			_module.Accusation.OnValueChanged += HandleAccusationChanged;
			_module.ReportStake.OnValueChanged += HandleStakeChanged;
			_module.LastReport.OnValueChanged += HandleReportChanged;

			Refresh();
		}

		protected override void OnUnbind()
		{
			if (_module != null)
			{
				_module.LastReport.OnValueChanged -= HandleReportChanged;
				_module.ReportStake.OnValueChanged -= HandleStakeChanged;
				_module.Accusation.OnValueChanged -= HandleAccusationChanged;
				Data.CurrentTurnClientId.OnValueChanged -= HandleTurnChanged;
				Data.OverlayStageId.OnValueChanged -= HandleOverlayChanged;
			}

			_module = null;

			if (_panel) _panel.SetActive(false);
		}

		private void HandleOverlayChanged(FixedString32Bytes previous, FixedString32Bytes current) => Refresh();
		private void HandleTurnChanged(ulong previous, ulong current) => Refresh();
		private void HandleStakeChanged(int previous, int current) => Refresh();
		private void HandleAccusationChanged(PokerReportAccusation previous, PokerReportAccusation current) => Refresh();
		private void HandleReportChanged(PokerReportResult previous, PokerReportResult current) => Refresh();

		private void Refresh()
		{
			var visible = IsReportOverlayRunning();

			if (_panel && _panel.activeSelf != visible) _panel.SetActive(visible);
			if (!visible) return;

			var accusation = _module.Accusation.Value;
			var report = _module.LastReport.Value;

			// The verdict answers the accusation by sequence — until it does, the table is still thinking.
			var judged = report.Sequence == accusation.Sequence;

			if (_accusationLabel) _accusationLabel.text = DescribeAccusation(accusation, judged);

			if (_verdictRoot && _verdictRoot.activeSelf != judged) _verdictRoot.SetActive(judged);

			if (judged && _verdictLabel)
			{
				_verdictLabel.text = !report.Called
					? $"{NameOf(report.AccuserClientId)} backed down — nobody paid to find out"
					: report.WasCheater
						? $"Guilty — {NameOf(report.TargetClientId)} pays {report.Amount} and folds"
						: $"Innocent — {NameOf(report.AccuserClientId)} pays {report.Amount} and plays on";
			}

			OnTick();
		}

		// Who is being asked, and for what: the accused for a number, then the accuser for the price of
		// seeing it. Read off the turn, because the turn is the one thing that says whose move it is.
		private string DescribeAccusation(PokerReportAccusation accusation, bool judged)
		{
			var accuser = NameOf(accusation.AccuserClientId);
			var target = NameOf(accusation.TargetClientId);
			var stake = _module.ReportStake.Value;

			if (judged || !Data.HasTurn) return $"{accuser} reports {target}";

			var turn = Data.CurrentTurnClientId.Value;

			if (turn == accusation.TargetClientId) return $"{accuser} reports {target} — {target} answers for it";
			if (turn == accusation.AccuserClientId) return $"{target} stakes {stake} on being clean — {accuser} to call";

			return $"{accuser} reports {target}";
		}

		protected override void OnTick()
		{
			// The duel runs on turn clocks and the verdict on the stage clock, and only ever one of them at
			// a time — whichever is running is this panel's countdown.
			var remaining = Data.HasTurn ? Data.TurnRemaining : Data.StageTimeRemaining;
			var normalized = Data.HasTurn ? Data.TurnNormalized : Data.StageTimeNormalized;

			if (_timerBar) _timerBar.SetTime(remaining, normalized);
		}

		private bool IsReportOverlayRunning()
		{
			var overlayId = Data.OverlayStageId.Value.ToString();
			if (string.IsNullOrEmpty(overlayId)) return false;

			return GameMode.FindStage(overlayId) is PokerReportStage;
		}

		private string NameOf(ulong clientId)
		{
			var player = PokerPlayer.Find(clientId);
			return player ? player.DisplayName : $"Player {clientId}";
		}
	}
}
