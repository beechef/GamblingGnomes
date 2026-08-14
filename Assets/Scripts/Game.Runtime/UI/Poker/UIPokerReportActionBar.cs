using Game.Runtime.GameMode.Poker;
using Game.Runtime.GameMode.Poker.Abilities;
using Game.Runtime.GameMode.Poker.Modules;
using Game.Runtime.GameMode.Poker.Player;
using Game.Runtime.GameMode.Poker.Stages;
using Game.Runtime.UI.Button;
using TMPro;
using Unity.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Runtime.UI.Poker
{
	// What the two people in an accusation are being asked for, which is nothing a street ever asks: the
	// accused names a number to stand behind, and the accuser either pays to see it or lets it go. Its own
	// bar rather than a second mode on the street's, so neither has to carry the other's rules — the street
	// bar stands down for the whole time an overlay is up.
	public class UIPokerReportActionBar : UIPokerView
	{
		[Header("Panel")]
		[SerializeField] private GameObject _panel;

		[Header("Defence")]
		[Tooltip("Shown to the accused. Answering is not optional, so there is no way out of here — only how big.")]
		[SerializeField] private GameObject _defenceRoot;

		[SerializeField] private Slider _stakeSlider;
		[SerializeField] private UIButton _betButton;
		[SerializeField] private TextMeshProUGUI _betLabel;
		[SerializeField] private UIButton _allInButton;
		[SerializeField] private TextMeshProUGUI _allInLabel;

		[Header("Response")]
		[Tooltip("Shown to the accuser. Two answers: the price of finding out, or nothing.")]
		[SerializeField] private GameObject _responseRoot;

		[SerializeField] private UIButton _callButton;
		[SerializeField] private TextMeshProUGUI _callLabel;
		[SerializeField] private UIButton _foldButton;

		[Header("Turn Timer")]
		[SerializeField] private Image _timerFill;
		[SerializeField] private TextMeshProUGUI _timerLabel;

		[Tooltip("What this player has to stake with — the number both sides of this are measured against.")]
		[SerializeField] private TextMeshProUGUI _chipsLabel;

		// Only the clock moves on its own, and only while this bar is the one being answered.
		protected override bool WantsTick => _panel && _panel.activeSelf;

		private PokerAbilityModule _module;

		private void Awake()
		{
			if (_panel) _panel.SetActive(false);
		}

		protected override void OnBind()
		{
			_module = GameMode.FindModule<PokerAbilityModule>();
			if (_module == null) return;

			if (_betButton) _betButton.OnClick += HandleBet;
			if (_allInButton) _allInButton.OnClick += HandleAllIn;
			if (_callButton) _callButton.OnClick += HandleCall;
			if (_foldButton) _foldButton.OnClick += HandleFold;
			if (_stakeSlider) _stakeSlider.onValueChanged.AddListener(HandleStakeChanged);

			Data.OverlayStageId.OnValueChanged += HandleStageChanged;
			Data.CurrentTurnClientId.OnValueChanged += HandleTurnChanged;
			_module.Accusation.OnValueChanged += HandleAccusationChanged;
			_module.ReportStake.OnValueChanged += HandleStakeValueChanged;
			LocalData.OnStateChanged += Refresh;

			Refresh();
		}

		protected override void OnUnbind()
		{
			if (_module != null)
			{
				LocalData.OnStateChanged -= Refresh;
				_module.ReportStake.OnValueChanged -= HandleStakeValueChanged;
				_module.Accusation.OnValueChanged -= HandleAccusationChanged;
				Data.CurrentTurnClientId.OnValueChanged -= HandleTurnChanged;
				Data.OverlayStageId.OnValueChanged -= HandleStageChanged;

				if (_stakeSlider) _stakeSlider.onValueChanged.RemoveListener(HandleStakeChanged);
				if (_foldButton) _foldButton.OnClick -= HandleFold;
				if (_callButton) _callButton.OnClick -= HandleCall;
				if (_allInButton) _allInButton.OnClick -= HandleAllIn;
				if (_betButton) _betButton.OnClick -= HandleBet;
			}

			_module = null;

			if (_panel) _panel.SetActive(false);
		}

		private void HandleStageChanged(FixedString32Bytes previous, FixedString32Bytes current) => Refresh();
		private void HandleTurnChanged(ulong previous, ulong current) => Refresh();
		private void HandleAccusationChanged(PokerReportAccusation previous, PokerReportAccusation current) => Refresh();
		private void HandleStakeValueChanged(int previous, int current) => Refresh();
		private void HandleStakeChanged(float value) => RefreshBetLabel();

		private void Refresh()
		{
			var stage = ResolveReportStage();
			var accusation = _module.Accusation.Value;

			// Which of the two is being asked comes off the turn: the report hands it to the accused first
			// and to the accuser second, and to nobody else at any point.
			var onTheClock = stage && IsLocalTurn;
			var defending = onTheClock && LocalClientId == accusation.TargetClientId;
			var answering = onTheClock && LocalClientId == accusation.AccuserClientId;

			var visible = defending || answering;
			if (_panel && _panel.activeSelf != visible) _panel.SetActive(visible);

			SetActive(_defenceRoot, defending);
			SetActive(_responseRoot, answering);

			if (!visible) return;

			if (_chipsLabel) _chipsLabel.text = LocalData.Chips.ToString();

			if (defending) RefreshDefence(stage, accusation);
			else RefreshResponse();

			RefreshTimer();
		}

		private void RefreshDefence(PokerReportStage stage, PokerReportAccusation accusation)
		{
			// The same ceiling the server clamps to, read off the same stage: neither of them can be offered
			// a number the other could not cover.
			var ceiling = stage.StakeCeiling(PokerPlayer.Find(accusation.TargetClientId),
				PokerPlayer.Find(accusation.AccuserClientId));

			var floor = stage.StakeFloor(ceiling);

			if (_stakeSlider)
			{
				_stakeSlider.minValue = floor;
				_stakeSlider.maxValue = Mathf.Max(floor, ceiling);
				_stakeSlider.wholeNumbers = true;
			}

			if (_betButton) _betButton.IsInteractable = true;
			if (_allInButton) _allInButton.IsInteractable = ceiling > 0;
			if (_allInLabel) _allInLabel.text = $"All-In {ceiling}";

			RefreshBetLabel();
		}

		private void RefreshResponse()
		{
			if (_callButton) _callButton.IsInteractable = true;
			if (_foldButton) _foldButton.IsInteractable = true;
			if (_callLabel) _callLabel.text = $"Call {_module.ReportStake.Value}";
		}

		private void RefreshBetLabel()
		{
			if (_betLabel) _betLabel.text = _stakeSlider ? $"Bet {(int)_stakeSlider.value}" : "Bet";
		}

		private void RefreshTimer()
		{
			if (_timerFill) _timerFill.fillAmount = Data.TurnNormalized;
			if (_timerLabel) _timerLabel.text = Mathf.CeilToInt(Data.TurnRemaining).ToString();
		}

		protected override void OnTick() => RefreshTimer();

		private PokerReportStage ResolveReportStage()
		{
			var overlayId = Data.OverlayStageId.Value.ToString();
			if (string.IsNullOrEmpty(overlayId)) return null;

			return GameMode.FindStage(overlayId) as PokerReportStage;
		}

		private static void SetActive(GameObject root, bool active)
		{
			if (root && root.activeSelf != active) root.SetActive(active);
		}

		private void Submit(PokerActionType action, int amount)
		{
			if (GameMode) GameMode.SubmitActionRPC(action, amount);
		}

		// With no slider wired the floor is what goes up: the server clamps whatever arrives, so zero is a
		// request for the smallest legal stake rather than a free pass.
		private void HandleBet() => Submit(PokerActionType.Bet, _stakeSlider ? (int)_stakeSlider.value : 0);

		private void HandleAllIn() => Submit(PokerActionType.AllIn, 0);
		private void HandleCall() => Submit(PokerActionType.Call, 0);
		private void HandleFold() => Submit(PokerActionType.Fold, 0);
	}
}
