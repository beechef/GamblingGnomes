using Game.Runtime.GameMode.Poker.Abilities;
using Game.Runtime.GameMode.Poker.Modules;
using Game.Runtime.GameMode.Poker.Player;
using Game.Runtime.GameMode.Poker.Stages;
using TMPro;
using Unity.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Runtime.UI.Poker
{
	// The report played out on screen: who is pointing at whom while the table sweats through the
	// thinking time, then the verdict when it falls. Up only while the report overlay runs — the
	// stage owns the pacing, this panel just mirrors it.
	public class UIPokerReportView : UIPokerView
	{
		[Header("Panel")]
		[SerializeField] private GameObject _panel;

		[Header("Accusation")]
		[SerializeField] private TextMeshProUGUI _accusationLabel;

		[Tooltip("Counts down the thinking time, then the verdict time — whatever clock the stage is running.")]
		[SerializeField] private TextMeshProUGUI _timerLabel;

		[SerializeField] private Image _timerFill;

		[Header("Verdict")]
		[SerializeField] private GameObject _verdictRoot;
		[SerializeField] private TextMeshProUGUI _verdictLabel;

		private PokerAbilityModule _module;

		protected override bool WantsTick => _panel && _panel.activeSelf && Data && Data.HasStageTimer;

		private void Awake()
		{
			if (_panel) _panel.SetActive(false);
		}

		protected override void OnBind()
		{
			_module = FindModule();
			if (_module == null) return;

			Data.OverlayStageId.OnValueChanged += HandleOverlayChanged;
			_module.Accusation.OnValueChanged += HandleAccusationChanged;
			_module.LastReport.OnValueChanged += HandleReportChanged;

			Refresh();
		}

		protected override void OnUnbind()
		{
			if (_module != null)
			{
				_module.LastReport.OnValueChanged -= HandleReportChanged;
				_module.Accusation.OnValueChanged -= HandleAccusationChanged;
				Data.OverlayStageId.OnValueChanged -= HandleOverlayChanged;
			}

			_module = null;

			if (_panel) _panel.SetActive(false);
		}

		private PokerAbilityModule FindModule()
		{
			foreach (var module in GameMode.Modules)
			{
				if (module is PokerAbilityModule abilityModule) return abilityModule;
			}

			return null;
		}

		private void HandleOverlayChanged(FixedString32Bytes previous, FixedString32Bytes current) => Refresh();
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

			if (_accusationLabel)
			{
				_accusationLabel.text = $"{NameOf(accusation.AccuserClientId)} reports {NameOf(accusation.TargetClientId)}";
			}

			if (_verdictRoot && _verdictRoot.activeSelf != judged) _verdictRoot.SetActive(judged);

			if (judged && _verdictLabel)
			{
				_verdictLabel.text = report.WasCheater
					? $"Guilty — {NameOf(report.TargetClientId)} was caught cheating (+{report.Amount})"
					: $"Innocent — {NameOf(report.AccuserClientId)} pays {report.Amount} and folds";
			}

			OnTick();
		}

		protected override void OnTick()
		{
			if (_timerLabel) _timerLabel.text = Mathf.CeilToInt(Data.StageTimeRemaining).ToString();
			if (_timerFill) _timerFill.fillAmount = Data.StageTimeNormalized;
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
