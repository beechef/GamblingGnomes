using Game.Runtime.GameMode.Poker;
using Game.Runtime.GameMode.Poker.Stages;
using Game.Runtime.UI.Button;
using TMPro;
using Unity.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Runtime.UI.Poker
{
	// The bar for a street that only asks whether the price is paid. It is its own bar rather than the
	// full one with three buttons greyed out, because what a player may do should read at a glance
	// instead of being deduced from what is dimmed — and a street with nothing to size has no business
	// carrying a slider. UIPokerActionBar stands down for the same stages this one stands up for, so
	// exactly one of them is ever on screen.
	public class UIPokerCallActionBar : UIPokerView
	{
		[Header("Panel")]
		[SerializeField] private GameObject _panel;

		[Header("Buttons")]
		[SerializeField] private UIButton _callButton;
		[SerializeField] private UIButton _foldButton;

		[Header("Labels")]
		[SerializeField] private TextMeshProUGUI _callLabel;
		[SerializeField] private TextMeshProUGUI _chipsLabel;

		[Header("Turn Timer")]
		[SerializeField] private Image _timerFill;
		[SerializeField] private TextMeshProUGUI _timerLabel;

		// The only thing here that changes every frame is the clock, and only while it is our turn.
		protected override bool WantsTick => IsLocalTurn;

		private void Awake()
		{
			if (_panel) _panel.SetActive(false);
		}

		protected override void OnBind()
		{
			if (_callButton) _callButton.OnClick += HandleCall;
			if (_foldButton) _foldButton.OnClick += HandleFold;

			Data.CurrentTurnClientId.OnValueChanged += HandleTurnChanged;
			Data.CurrentBet.OnValueChanged += HandleBetChanged;
			Data.StageId.OnValueChanged += HandleStageChanged;
			Data.OverlayStageId.OnValueChanged += HandleStageChanged;
			LocalData.OnStateChanged += Refresh;

			Refresh();
		}

		protected override void OnUnbind()
		{
			if (_callButton) _callButton.OnClick -= HandleCall;
			if (_foldButton) _foldButton.OnClick -= HandleFold;

			Data.CurrentTurnClientId.OnValueChanged -= HandleTurnChanged;
			Data.CurrentBet.OnValueChanged -= HandleBetChanged;
			Data.StageId.OnValueChanged -= HandleStageChanged;
			Data.OverlayStageId.OnValueChanged -= HandleStageChanged;
			LocalData.OnStateChanged -= Refresh;

			if (_panel) _panel.SetActive(false);
		}

		private void HandleTurnChanged(ulong previous, ulong current) => Refresh();
		private void HandleBetChanged(int previous, int current) => Refresh();
		private void HandleStageChanged(FixedString32Bytes previous, FixedString32Bytes current) => Refresh();

		private void Refresh()
		{
			// While something is overlaid on the street the turn belongs to it, and whoever is on that
			// clock is being asked something else entirely.
			var stage = GameMode.FindStage(Data.StageId.Value.ToString()) as PokerBettingStage;
			var ourTurn = stage && stage.IsCallOnly && IsLocalTurn && LocalData.CanAct && Data.OverlayStageId.Value.IsEmpty;

			if (_panel && _panel.activeSelf != ourTurn) _panel.SetActive(ourTurn);
			if (!ourTurn) return;

			var owed = Mathf.Max(0, Data.CurrentBet.Value - LocalData.Bet.Value);

			// Short of the price still calls — the server takes whatever is left and puts the player all
			// in for it, which is worth more to them than the fold this would otherwise force.
			if (_callButton) _callButton.IsInteractable = owed > 0;
			if (_callLabel) _callLabel.text = owed > 0 ? $"Call {Mathf.Min(owed, LocalData.Chips)}" : "Call";
			if (_chipsLabel) _chipsLabel.text = LocalData.Chips.ToString();

			RefreshTimer();
		}

		private void RefreshTimer()
		{
			if (_timerFill) _timerFill.fillAmount = Data.TurnNormalized;
			if (_timerLabel) _timerLabel.text = Mathf.CeilToInt(Data.TurnRemaining).ToString();
		}

		protected override void OnTick() => RefreshTimer();

		private void HandleCall()
		{
			if (GameMode) GameMode.SubmitActionRPC(PokerActionType.Call, 0);
		}

		private void HandleFold()
		{
			if (GameMode) GameMode.SubmitActionRPC(PokerActionType.Fold, 0);
		}
	}
}
