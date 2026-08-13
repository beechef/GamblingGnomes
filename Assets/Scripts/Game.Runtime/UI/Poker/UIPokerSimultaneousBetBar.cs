using Game.Runtime.GameMode.Poker;
using Game.Runtime.GameMode.Poker.Stages;
using Game.Runtime.UI.Button;
using TMPro;
using Unity.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Runtime.UI.Poker
{
	// The action bar's counterpart for a stage nobody takes turns in: it is up the whole time the stage
	// runs, swaps to a waiting state the moment this player has answered, and takes its limits from the
	// running stage asset so two simultaneous streets can ask for very different bets.
	public class UIPokerSimultaneousBetBar : UIPokerView
	{
		[Header("Panels")]
		[SerializeField] private GameObject _panel;

		[Tooltip("Shown instead of the panel once this player has bet and the table is still deciding.")]
		[SerializeField] private GameObject _waitingPanel;

		[Header("Buttons")]
		[SerializeField] private UIButton _betButton;
		[SerializeField] private UIButton _foldButton;

		[Header("Amount")]
		[SerializeField] private Slider _amountSlider;
		[SerializeField] private TextMeshProUGUI _betLabel;
		[SerializeField] private TextMeshProUGUI _chipsLabel;

		[Header("Status")]
		[SerializeField] private TextMeshProUGUI _lockedInLabel;

		[Header("Timer")]
		[SerializeField] private Image _timerFill;
		[SerializeField] private TextMeshProUGUI _timerLabel;

		private PokerSimultaneousBetStage _stage;

		protected override bool WantsTick => _stage && Data && Data.HasStageTimer;

		private void Awake()
		{
			if (_panel) _panel.SetActive(false);
			if (_waitingPanel) _waitingPanel.SetActive(false);
		}

		protected override void OnBind()
		{
			if (_betButton) _betButton.OnClick += HandleBet;
			if (_foldButton) _foldButton.OnClick += HandleFold;
			if (_amountSlider) _amountSlider.onValueChanged.AddListener(HandleAmountChanged);

			Data.StageId.OnValueChanged += HandleStageChanged;
			Data.OverlayStageId.OnValueChanged += HandleStageChanged;
			LocalData.OnStateChanged += Refresh;

			Refresh();
		}

		protected override void OnUnbind()
		{
			if (_betButton) _betButton.OnClick -= HandleBet;
			if (_foldButton) _foldButton.OnClick -= HandleFold;
			if (_amountSlider) _amountSlider.onValueChanged.RemoveListener(HandleAmountChanged);

			Data.OverlayStageId.OnValueChanged -= HandleStageChanged;
			Data.StageId.OnValueChanged -= HandleStageChanged;
			LocalData.OnStateChanged -= Refresh;

			_stage = null;

			if (_panel) _panel.SetActive(false);
			if (_waitingPanel) _waitingPanel.SetActive(false);
		}

		private void HandleStageChanged(FixedString32Bytes previous, FixedString32Bytes current) => Refresh();
		private void HandleAmountChanged(float value) => RefreshBetLabel();

		private void Refresh()
		{
			// Every peer runs the same stage list, so the bar reads the running stage's own numbers
			// rather than having them mirrored through the table data.
			_stage = GameMode.FindStage(Data.StageId.Value.ToString()) as PokerSimultaneousBetStage;

			// An overlay pauses the stage underneath, and the server drops anything sent to a paused
			// stage — so the bar stops offering what would only be refused.
			var overlaid = !Data.OverlayStageId.Value.IsEmpty;

			var awaitingUs = _stage && !overlaid && LocalData.CanAct && !LocalData.HasActed.Value;

			// Only somebody still in the hand is waiting on anything — a folded or busted seat is out.
			var waiting = _stage && !overlaid && !awaitingUs && LocalData.IsInHand;

			if (_panel && _panel.activeSelf != awaitingUs) _panel.SetActive(awaitingUs);
			if (_waitingPanel && _waitingPanel.activeSelf != waiting) _waitingPanel.SetActive(waiting);

			if (!_stage) return;

			if (awaitingUs) RefreshAmounts();

			RefreshLockedIn();
			RefreshTimer();
		}

		private void RefreshAmounts()
		{
			var chips = LocalData.Chips;
			var minimum = _stage.MinimumBetFor(chips);
			var maximum = _stage.MaximumBetFor(chips);

			if (_amountSlider)
			{
				_amountSlider.wholeNumbers = true;
				_amountSlider.minValue = minimum;
				_amountSlider.maxValue = Mathf.Max(minimum, maximum);
				_amountSlider.interactable = maximum > minimum;
			}

			if (_betButton) _betButton.IsInteractable = maximum > 0;
			if (_foldButton) _foldButton.gameObject.SetActive(_stage.AllowFold);
			if (_chipsLabel) _chipsLabel.text = chips.ToString();

			RefreshBetLabel();
		}

		private void RefreshBetLabel()
		{
			if (_betLabel) _betLabel.text = $"Bet {SelectedAmount()}";
		}

		private void RefreshLockedIn()
		{
			if (!_lockedInLabel) return;

			var total = 0;
			var lockedIn = 0;

			foreach (var player in GameMode.SeatedPlayers)
			{
				if (!player.Data.IsInHand) continue;

				total++;
				if (!player.Data.CanAct || player.Data.HasActed.Value) lockedIn++;
			}

			_lockedInLabel.text = $"{lockedIn}/{total}";
		}

		private void RefreshTimer()
		{
			if (_timerFill) _timerFill.fillAmount = Data.StageTimeNormalized;
			if (_timerLabel) _timerLabel.text = Data.HasStageTimer ? Mathf.CeilToInt(Data.StageTimeRemaining).ToString() : string.Empty;
		}

		protected override void OnTick()
		{
			RefreshTimer();
			RefreshLockedIn();
		}

		private int SelectedAmount()
		{
			if (!_stage || !LocalData) return 0;

			var requested = _amountSlider ? (int)_amountSlider.value : _stage.MinimumBet;
			return _stage.ClampBet(requested, LocalData.Chips);
		}

		private void HandleBet()
		{
			if (GameMode) GameMode.SubmitActionRPC(PokerActionType.Bet, SelectedAmount());
		}

		private void HandleFold()
		{
			if (GameMode) GameMode.SubmitActionRPC(PokerActionType.Fold, 0);
		}
	}
}
