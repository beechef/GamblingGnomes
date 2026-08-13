using Game.Runtime.GameMode.Poker;
using Game.Runtime.GameMode.Poker.Stages;
using TMPro;
using Unity.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Runtime.UI.Poker
{
	// The clock for stages that play out on their own — a reveal holding the table, the deal, the
	// counting. Without it a timed stage with no turns reads as a hang. It stays out of the way of
	// every view that owns its own clock: the turn banner's turn, the bet bar's street, the report
	// overlay's verdict.
	public class UIPokerStageTimer : UIPokerView
	{
		[Header("References")]
		[SerializeField] private GameObject _panel;
		[SerializeField] private TextMeshProUGUI _phaseLabel;
		[SerializeField] private TextMeshProUGUI _timerLabel;
		[SerializeField] private Image _timerFill;

		protected override bool WantsTick => _panel && _panel.activeSelf;

		private void Awake()
		{
			if (_panel) _panel.SetActive(false);
		}

		protected override void OnBind()
		{
			Data.StageDuration.OnValueChanged += HandleDurationChanged;
			Data.CurrentTurnClientId.OnValueChanged += HandleTurnChanged;
			Data.Phase.OnValueChanged += HandlePhaseChanged;
			Data.StageId.OnValueChanged += HandleStageChanged;
			Data.OverlayStageId.OnValueChanged += HandleStageChanged;

			Refresh();
		}

		protected override void OnUnbind()
		{
			Data.OverlayStageId.OnValueChanged -= HandleStageChanged;
			Data.StageId.OnValueChanged -= HandleStageChanged;
			Data.Phase.OnValueChanged -= HandlePhaseChanged;
			Data.CurrentTurnClientId.OnValueChanged -= HandleTurnChanged;
			Data.StageDuration.OnValueChanged -= HandleDurationChanged;

			if (_panel) _panel.SetActive(false);
		}

		private void HandleDurationChanged(float previous, float current) => Refresh();
		private void HandleTurnChanged(ulong previous, ulong current) => Refresh();
		private void HandlePhaseChanged(PokerPhase previous, PokerPhase current) => Refresh();
		private void HandleStageChanged(FixedString32Bytes previous, FixedString32Bytes current) => Refresh();

		private void Refresh()
		{
			// The simultaneous street draws its own countdown, so this one bows out rather than
			// showing the same clock twice.
			var visible = Data.HasStageTimer
			              && !Data.HasTurn
			              && Data.OverlayStageId.Value.IsEmpty
			              && GameMode.FindStage(Data.StageId.Value.ToString()) is not PokerSimultaneousBetStage;

			if (_panel && _panel.activeSelf != visible) _panel.SetActive(visible);
			if (!visible) return;

			if (_phaseLabel) _phaseLabel.text = Data.Phase.Value.ToString();

			OnTick();
		}

		protected override void OnTick()
		{
			if (_timerLabel) _timerLabel.text = Mathf.CeilToInt(Data.StageTimeRemaining).ToString();
			if (_timerFill) _timerFill.fillAmount = Data.StageTimeNormalized;
		}
	}
}
