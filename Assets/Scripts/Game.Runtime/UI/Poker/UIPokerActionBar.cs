using Game.Runtime.GameMode.Poker;
using Game.Runtime.GameMode.Poker.Stages;
using Game.Runtime.UI.Button;
using TMPro;
using Unity.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Runtime.UI.Poker
{
	public class UIPokerActionBar : UIPokerView
	{
		[Header("Panel")]
		[SerializeField] private GameObject _panel;

		[Header("Buttons")]
		[SerializeField] private UIButton _foldButton;
		[SerializeField] private UIButton _checkButton;
		[SerializeField] private UIButton _callButton;
		[SerializeField] private UIButton _raiseButton;
		[SerializeField] private UIButton _allInButton;

		[Header("Labels")]
		[SerializeField] private TextMeshProUGUI _callLabel;
		[SerializeField] private TextMeshProUGUI _raiseLabel;
		[SerializeField] private TextMeshProUGUI _chipsLabel;

		[Header("Raise")]
		[SerializeField] private Slider _raiseSlider;

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
			if (_foldButton) _foldButton.OnClick += HandleFold;
			if (_checkButton) _checkButton.OnClick += HandleCheck;
			if (_callButton) _callButton.OnClick += HandleCall;
			if (_raiseButton) _raiseButton.OnClick += HandleRaise;
			if (_allInButton) _allInButton.OnClick += HandleAllIn;
			if (_raiseSlider) _raiseSlider.onValueChanged.AddListener(HandleRaiseAmountChanged);

			Data.CurrentTurnClientId.OnValueChanged += HandleTurnChanged;
			Data.CurrentBet.OnValueChanged += HandleBetChanged;
			Data.StageId.OnValueChanged += HandleStageChanged;
			LocalData.OnStateChanged += Refresh;

			Refresh();
		}

		protected override void OnUnbind()
		{
			if (_foldButton) _foldButton.OnClick -= HandleFold;
			if (_checkButton) _checkButton.OnClick -= HandleCheck;
			if (_callButton) _callButton.OnClick -= HandleCall;
			if (_raiseButton) _raiseButton.OnClick -= HandleRaise;
			if (_allInButton) _allInButton.OnClick -= HandleAllIn;
			if (_raiseSlider) _raiseSlider.onValueChanged.RemoveListener(HandleRaiseAmountChanged);

			Data.CurrentTurnClientId.OnValueChanged -= HandleTurnChanged;
			Data.CurrentBet.OnValueChanged -= HandleBetChanged;
			Data.StageId.OnValueChanged -= HandleStageChanged;
			LocalData.OnStateChanged -= Refresh;

			if (_panel) _panel.SetActive(false);
		}

		private void HandleTurnChanged(ulong previous, ulong current) => Refresh();
		private void HandleBetChanged(int previous, int current) => Refresh();
		private void HandleStageChanged(FixedString32Bytes previous, FixedString32Bytes current) => Refresh();
		private void HandleRaiseAmountChanged(float value) => RefreshRaiseLabel();

		private void Refresh()
		{
			var ourTurn = IsLocalTurn && LocalData.CanAct;

			if (_panel && _panel.activeSelf != ourTurn) _panel.SetActive(ourTurn);
			if (!ourTurn) return;

			var owed = Mathf.Max(0, Data.CurrentBet.Value - LocalData.Bet.Value);

			// What a raise costs is the running street's business, so the bar asks it rather than
			// carrying a second copy of the numbers.
			var stage = GameMode.FindStage(Data.StageId.Value.ToString()) as PokerBettingStage;
			var minimumTarget = Data.CurrentBet.Value + (stage ? stage.MinimumRaiseStep : Data.LastRaise.Value);
			var maximumTarget = LocalData.Bet.Value + LocalData.Chips.Value;

			if (_checkButton) _checkButton.IsInteractable = owed <= 0 && (!stage || stage.AllowCheckWhenNoBet);
			if (_callButton) _callButton.IsInteractable = owed > 0 && LocalData.Chips.Value > 0;
			if (_raiseButton) _raiseButton.IsInteractable = maximumTarget > minimumTarget;
			if (_allInButton) _allInButton.IsInteractable = LocalData.Chips.Value > 0 && (!stage || stage.AllowAllIn);

			if (_raiseSlider)
			{
				_raiseSlider.minValue = minimumTarget;
				_raiseSlider.maxValue = Mathf.Max(minimumTarget, maximumTarget);
				_raiseSlider.wholeNumbers = true;
			}

			if (_callLabel) _callLabel.text = owed > 0 ? $"Call {Mathf.Min(owed, LocalData.Chips.Value)}" : "Call";
			if (_chipsLabel) _chipsLabel.text = LocalData.Chips.Value.ToString();

			RefreshRaiseLabel();
			RefreshTimer();
		}

		private void RefreshRaiseLabel()
		{
			if (_raiseLabel) _raiseLabel.text = _raiseSlider ? $"Raise to {(int)_raiseSlider.value}" : "Raise";
		}

		private void RefreshTimer()
		{
			if (_timerFill) _timerFill.fillAmount = Data.TurnNormalized;
			if (_timerLabel) _timerLabel.text = Mathf.CeilToInt(Data.TurnRemaining).ToString();
		}

		protected override void OnTick() => RefreshTimer();

		private void Submit(PokerActionType action, int amount)
		{
			if (GameMode) GameMode.SubmitActionRPC(action, amount);
		}

		private void HandleFold() => Submit(PokerActionType.Fold, 0);
		private void HandleCheck() => Submit(PokerActionType.Check, 0);
		private void HandleCall() => Submit(PokerActionType.Call, 0);
		private void HandleAllIn() => Submit(PokerActionType.AllIn, 0);
		private void HandleRaise() => Submit(PokerActionType.Raise, _raiseSlider ? (int)_raiseSlider.value : 0);
	}
}
