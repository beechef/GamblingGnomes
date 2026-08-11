using Game.Runtime.GameMode.Poker;
using Game.Runtime.UI.Button;
using TMPro;
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
		}

		protected override void OnUnbind()
		{
			if (_foldButton) _foldButton.OnClick -= HandleFold;
			if (_checkButton) _checkButton.OnClick -= HandleCheck;
			if (_callButton) _callButton.OnClick -= HandleCall;
			if (_raiseButton) _raiseButton.OnClick -= HandleRaise;
			if (_allInButton) _allInButton.OnClick -= HandleAllIn;
			if (_panel) _panel.SetActive(false);
		}

		protected override void OnTick()
		{
			var localPlayer = GameMode.FindSeatedPlayer(LocalClientId);
			var hasTurn = IsLocalTurn && localPlayer && localPlayer.Data.CanAct;
			if (_panel && _panel.activeSelf != hasTurn) _panel.SetActive(hasTurn);
			if (!hasTurn) return;

			var self = localPlayer.Data;

			var owed = Mathf.Max(0, Data.CurrentBet.Value - self.Bet.Value);
			var rules = GameMode.Rules;
			var minimumRaise = rules ? Mathf.Max(rules.BigBlind, Data.LastRaise.Value * rules.MinimumRaiseMultiplier) : Data.LastRaise.Value;
			var minimumTarget = Data.CurrentBet.Value + minimumRaise;
			var maximumTarget = self.Bet.Value + self.Chips.Value;

			if (_checkButton) _checkButton.IsInteractable = owed <= 0;
			if (_callButton) _callButton.IsInteractable = owed > 0 && self.Chips.Value > 0;
			if (_raiseButton) _raiseButton.IsInteractable = maximumTarget > minimumTarget;
			if (_allInButton) _allInButton.IsInteractable = self.Chips.Value > 0 && (!rules || rules.AllowAllIn);

			if (_raiseSlider)
			{
				_raiseSlider.minValue = minimumTarget;
				_raiseSlider.maxValue = Mathf.Max(minimumTarget, maximumTarget);
				_raiseSlider.wholeNumbers = true;
			}

			if (_callLabel) _callLabel.text = owed > 0 ? $"Call {Mathf.Min(owed, self.Chips.Value)}" : "Call";
			if (_raiseLabel) _raiseLabel.text = _raiseSlider ? $"Raise to {(int)_raiseSlider.value}" : "Raise";
			if (_chipsLabel) _chipsLabel.text = self.Chips.Value.ToString();

			var normalized = Data.TurnNormalized;
			if (_timerFill) _timerFill.fillAmount = normalized;
			if (_timerLabel) _timerLabel.text = Mathf.CeilToInt(Data.TurnRemaining).ToString();
		}

		private void Submit(PokerActionType action, int amount)
		{
			if (!GameMode) return;

			GameMode.SubmitActionRPC(action, amount);
		}

		private void HandleFold() => Submit(PokerActionType.Fold, 0);
		private void HandleCheck() => Submit(PokerActionType.Check, 0);
		private void HandleCall() => Submit(PokerActionType.Call, 0);
		private void HandleAllIn() => Submit(PokerActionType.AllIn, 0);
		private void HandleRaise() => Submit(PokerActionType.Raise, _raiseSlider ? (int)_raiseSlider.value : 0);
	}
}
