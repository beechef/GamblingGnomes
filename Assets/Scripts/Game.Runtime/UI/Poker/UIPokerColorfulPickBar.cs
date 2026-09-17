using Game.Runtime.GameMode.Poker.Stages;
using Game.Runtime.UI.Progress;
using Unity.Collections;
using UnityEngine;

namespace Game.Runtime.UI.Poker
{
	// The clock on the winner choosing who eats the Colorful cap. The choice itself has no buttons here:
	// everybody else is pointed at across the table (PokerColorfulPickController) and the winner's own name
	// sits on their hallucination bar (UIPokerColorfulSelfPick).
	public class UIPokerColorfulPickBar : UIPokerView
	{
		[Header("Panel")]
		[SerializeField] private GameObject _panel;

		[Header("Turn Timer")]
		[Tooltip("Hidden outright when the stage runs no clock, rather than drawn sitting at zero.")]
		[SerializeField] private UITimerBar _timerBar;

		protected override bool WantsTick => IsLocalTurn && Data && Data.HasTurnClock;

		private void Awake()
		{
			if (_panel) _panel.SetActive(false);
		}

		protected override void OnBind()
		{
			Data.CurrentTurnClientId.OnValueChanged += HandleTurnChanged;
			Data.StageId.OnValueChanged += HandleStageChanged;

			Refresh();
		}

		protected override void OnUnbind()
		{
			Data.StageId.OnValueChanged -= HandleStageChanged;
			Data.CurrentTurnClientId.OnValueChanged -= HandleTurnChanged;

			if (_panel) _panel.SetActive(false);
		}

		protected override void OnTick()
		{
			if (!_timerBar) return;

			_timerBar.SetTime(Data.TurnRemaining, Data.TurnNormalized);
		}

		private void HandleTurnChanged(ulong previous, ulong current) => Refresh();
		private void HandleStageChanged(FixedString32Bytes previous, FixedString32Bytes current) => Refresh();

		// Resolved from the replicated stage id: GameMode.CurrentStage is written only by the server's own
		// stage machine and is null on a client for the whole session.
		private void Refresh()
		{
			var stage = GameMode ? GameMode.FindStage(Data.StageId.Value.ToString()) as PokerColorfulPickStage : null;
			var show = stage != null && IsLocalTurn;

			if (_panel) _panel.SetActive(show);
			if (show && _timerBar) _timerBar.gameObject.SetActive(Data.HasTurnClock);
		}
	}
}
