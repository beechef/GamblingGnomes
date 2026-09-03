using Game.Runtime.GameMode.Poker;
using Game.Runtime.GameMode.Poker.Stages;
using Game.Runtime.UI.Button;
using Game.Runtime.UI.Progress;
using TMPro;
using Unity.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Runtime.UI.Poker
{
	// What the street is asking this player for, on their clock. Only the street — anything overlaid on it
	// asks its own questions and brings its own bar, so this one stands down rather than offering street
	// actions to a stage that is not listening for them.
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
		[SerializeField] private UITimerBar _timerBar;

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
			Data.OverlayStageId.OnValueChanged += HandleStageChanged;
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
			Data.OverlayStageId.OnValueChanged -= HandleStageChanged;
			LocalData.OnStateChanged -= Refresh;

			if (_panel) _panel.SetActive(false);
		}

		private void HandleTurnChanged(ulong previous, ulong current) => Refresh();
		private void HandleBetChanged(int previous, int current) => Refresh();
		private void HandleStageChanged(FixedString32Bytes previous, FixedString32Bytes current) => Refresh();
		private void HandleRaiseAmountChanged(float value) => RefreshRaiseLabel();

		private void Refresh()
		{
			// What a raise costs is the running street's business, so the bar asks it rather than
			// carrying a second copy of the numbers.
			var stage = GameMode.FindStage(Data.StageId.Value.ToString()) as PokerBettingStage;

			// While something is overlaid on the street the turn belongs to it, and whoever is on that clock
			// is being asked something else entirely. A street that only asks call or fold has a bar of its
			// own, so this one steps aside rather than showing two buttons out of five — and a stage that is
			// not a betting street at all has no verbs for this bar to offer, so no stage means no bar
			// rather than a bar running on a rulebook of its own.
			var ourTurn = IsLocalTurn && LocalData.CanAct && Data.OverlayStageId.Value.IsEmpty
			              && stage && !stage.IsCallOnly;

			if (_panel && _panel.activeSelf != ourTurn) _panel.SetActive(ourTurn);
			if (!ourTurn) return;

			// Every number and every permission comes off the running stage, never re-derived here: the
			// stage is what the server accepts with, and a second copy is a copy that drifts.
			var owed = stage.OwedBy(LocalData);
			var minimumTarget = stage.MinimumRaiseTarget;
			var maximumTarget = stage.MaximumRaiseTargetFor(LocalData);

			// What the street forbids is hidden; what this player merely cannot afford is dimmed. A greyed
			// button says "not now", a missing one says "not here", and a street that never allows raising
			// should not spend the space claiming otherwise.
			SetShown(_checkButton, stage.AllowCheckWhenNoBet);
			SetShown(_raiseButton, stage.AllowRaise);
			SetShown(_allInButton, stage.AllowAllIn);
			if (_raiseSlider) _raiseSlider.gameObject.SetActive(stage.AllowRaise);

			if (_checkButton) _checkButton.IsInteractable = stage.CanCheck(LocalData);
			if (_callButton) _callButton.IsInteractable = stage.CanCall(LocalData);
			if (_raiseButton) _raiseButton.IsInteractable = stage.CanRaise(LocalData);
			if (_allInButton) _allInButton.IsInteractable = stage.CanAllIn(LocalData);

			if (_raiseSlider && stage.AllowRaise)
			{
				_raiseSlider.interactable = stage.CanRaise(LocalData);
				_raiseSlider.minValue = minimumTarget;
				_raiseSlider.maxValue = Mathf.Max(minimumTarget, maximumTarget);
				_raiseSlider.wholeNumbers = true;
			}

			if (_callLabel) _callLabel.text = owed > 0 ? $"Call {stage.CallCostFor(LocalData)}" : "Call";
			if (_chipsLabel) _chipsLabel.text = LocalData.Chips.ToString();

			RefreshRaiseLabel();
			RefreshTimer();
		}

		private static void SetShown(UIButton button, bool shown)
		{
			if (button && button.gameObject.activeSelf != shown) button.gameObject.SetActive(shown);
		}

		private void RefreshRaiseLabel()
		{
			if (_raiseLabel) _raiseLabel.text = _raiseSlider ? $"Raise to {(int)_raiseSlider.value}" : "Raise";
		}

		private void RefreshTimer()
		{
			if (_timerBar) _timerBar.SetTime(Data.TurnRemaining, Data.TurnNormalized);
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
