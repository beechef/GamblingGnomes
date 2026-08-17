using Game.Runtime.GameMode.Poker;
using Game.Runtime.GameMode.Poker.Stages;
using Game.Runtime.Player;
using Game.Runtime.UI.Progress;
using TMPro;
using Unity.Collections;
using UnityEngine;

namespace Game.Runtime.UI.Poker
{
	// One clock for the table, under the board: whose turn it is and how long they have, or — when no
	// seat is on the clock — which stage is playing itself out. Two widgets showing two countdowns was
	// the confusing part, because a player cannot tell at a glance which of them is theirs.
	public class UIPokerTurnPanel : UIPokerView
	{
		[Header("Panel")]
		[SerializeField] private GameObject _panel;

		[Header("Labels")]
		[SerializeField] private TextMeshProUGUI _titleLabel;

		[Header("Timer")]
		[SerializeField] private UITimerBar _timerBar;

		[Header("Colours")]
		[SerializeField] private Color _localTurnColor = new(0.35f, 0.85f, 0.4f);
		[SerializeField] private Color _remoteTurnColor = new(0.9f, 0.75f, 0.3f);
		[SerializeField] private Color _stageColor = new(0.85f, 0.82f, 0.72f);

		// Only the clock moves on its own, and only while there is a clock to show.
		protected override bool WantsTick => _panel && _panel.activeSelf;

		private PlayerData _boundWallet;

		private void Awake()
		{
			if (_panel) _panel.SetActive(false);
		}

		protected override void OnBind()
		{
			Data.CurrentTurnClientId.OnValueChanged += HandleTurnChanged;
			Data.StageDuration.OnValueChanged += HandleDurationChanged;
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
			Data.StageDuration.OnValueChanged -= HandleDurationChanged;
			Data.CurrentTurnClientId.OnValueChanged -= HandleTurnChanged;

			BindTurnIdentity(null);

			if (_panel) _panel.SetActive(false);
		}

		private void HandleTurnChanged(ulong previous, ulong current) => Refresh();
		private void HandleDurationChanged(float previous, float current) => Refresh();
		private void HandlePhaseChanged(PokerPhase previous, PokerPhase current) => Refresh();
		private void HandleStageChanged(FixedString32Bytes previous, FixedString32Bytes current) => Refresh();

		// The panel names one player at a time, so it listens to that player only and moves the
		// subscription along with the turn — a name landing late still reaches the label.
		private void BindTurnIdentity(PlayerData wallet)
		{
			if (_boundWallet == wallet) return;

			if (_boundWallet) _boundWallet.OnIdentityChanged -= HandleIdentityChanged;

			_boundWallet = wallet;

			if (_boundWallet) _boundWallet.OnIdentityChanged += HandleIdentityChanged;
		}

		private void HandleIdentityChanged() => Refresh();

		private void Refresh()
		{
			var visible = Data.HasTurn || ShowsStageClock();

			if (_panel && _panel.activeSelf != visible) _panel.SetActive(visible);

			if (!visible)
			{
				BindTurnIdentity(null);
				return;
			}

			if (Data.HasTurn) RefreshTurn();
			else RefreshStage();

			OnTick();
		}

		// The simultaneous street draws its own countdown on the bet bar, so this one bows out rather
		// than showing the same clock twice.
		private bool ShowsStageClock() =>
			Data.HasStageTimer
			&& Data.OverlayStageId.Value.IsEmpty
			&& GameMode.FindStage(Data.StageId.Value.ToString()) is not PokerSimultaneousBetStage;

		private void RefreshTurn()
		{
			var turnClientId = Data.CurrentTurnClientId.Value;
			var turnPlayer = GameMode.FindSeatedPlayer(turnClientId);

			BindTurnIdentity(turnPlayer ? turnPlayer.Wallet : null);

			if (!_titleLabel) return;

			_titleLabel.text = turnClientId == LocalClientId
				? "YOUR TURN"
				: $"{(turnPlayer ? turnPlayer.DisplayName : "Player")}'S TURN";
		}

		private void RefreshStage()
		{
			BindTurnIdentity(null);

			if (_titleLabel) _titleLabel.text = Data.Phase.Value.ToString().ToUpperInvariant();
		}

		protected override void OnTick()
		{
			if (!_timerBar) return;

			var onTurn = Data.HasTurn;

			// The bar draws whose clock this is; the urgent tone near zero is the widget's own business.
			_timerBar.BarColor = onTurn ? (IsLocalTurn ? _localTurnColor : _remoteTurnColor) : _stageColor;
			_timerBar.SetTime(
				onTurn ? Data.TurnRemaining : Data.StageTimeRemaining,
				onTurn ? Data.TurnNormalized : Data.StageTimeNormalized);
		}
	}
}
