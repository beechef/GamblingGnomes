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
			// An overlay runs its own clock over its own question, in the same place and the same style —
			// so this one leaves rather than being buried under it. The overlay hands out turns of its own,
			// which is why the turn alone is not enough to decide this: "X'S TURN" is true during an
			// accusation and is not what the table needs to be reading.
			var visible = (Data.HasTurn || ShowsStageClock()) && Data.OverlayStageId.Value.IsEmpty;

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
			_timerBar.SetTime(
				onTurn ? Data.TurnRemaining : Data.StageTimeRemaining,
				onTurn ? Data.TurnNormalized : Data.StageTimeNormalized);
		}
	}
}
