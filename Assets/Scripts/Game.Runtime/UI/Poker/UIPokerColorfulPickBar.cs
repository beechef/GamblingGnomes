using Game.Runtime.GameMode.Poker;
using Game.Runtime.GameMode.Poker.Stages;
using Game.Runtime.UI.Button;
using Game.Runtime.UI.Progress;
using TMPro;
using Unity.Collections;
using UnityEngine;

namespace Game.Runtime.UI.Poker
{
	// The winner naming themselves as the one who eats the Colorful cap. Everybody else is picked by pointing
	// at them across the table (PokerColorfulPickController); your own body is under your own eye and hidden
	// from you, so it is the one choice that cannot be made in the world and gets a button with your name.
	public class UIPokerColorfulPickBar : UIPokerView
	{
		[Header("Panel")]
		[SerializeField] private GameObject _panel;

		[Header("Buttons")]
		[Tooltip("Button prefab cloned for the local player.")]
		[SerializeField] private UIButton _buttonPrefab;

		[Tooltip("Row the button is laid into.")]
		[SerializeField] private RectTransform _buttonRow;

		[Header("Turn Timer")]
		[Tooltip("Hidden outright when the stage runs no clock, rather than drawn sitting at zero.")]
		[SerializeField] private UITimerBar _timerBar;

		private UIButton _button;

		protected override bool WantsTick => IsLocalTurn && Data && Data.HasTurnClock;

		private void Awake()
		{
			if (_panel) _panel.SetActive(false);
		}

		protected override void OnBind()
		{
			Data.CurrentTurnClientId.OnValueChanged += HandleTurnChanged;
			Data.StageId.OnValueChanged += HandleStageChanged;
			Data.OverlayStageId.OnValueChanged += HandleStageChanged;

			Refresh();
		}

		protected override void OnUnbind()
		{
			Data.OverlayStageId.OnValueChanged -= HandleStageChanged;
			Data.StageId.OnValueChanged -= HandleStageChanged;
			Data.CurrentTurnClientId.OnValueChanged -= HandleTurnChanged;

			ClearButton();

			if (_panel) _panel.SetActive(false);
		}

		protected override void OnTick()
		{
			if (!_timerBar) return;

			_timerBar.SetTime(Data.TurnRemaining, Data.TurnNormalized);
		}

		private void HandleTurnChanged(ulong previous, ulong current) => Refresh();
		private void HandleStageChanged(FixedString32Bytes previous, FixedString32Bytes current) => Refresh();

		private void Refresh()
		{
			// Resolved from the replicated stage id: GameMode.ActiveStage is written only by the server's
			// own stage machine and is null on a client for the whole session.
			var stage = GameMode ? GameMode.FindStage(Data.StageId.Value.ToString()) as PokerColorfulPickStage : null;
			var show = stage != null && IsLocalTurn && Data.OverlayStageId.Value.IsEmpty && stage.CanBeFed(LocalPlayer);

			if (_panel) _panel.SetActive(show);

			// Built once as the bar comes up rather than on every refresh: re-instantiating a button under the
			// pointer is how a click lands on nothing.
			if (show && !_button) BuildButton();
			else if (!show) ClearButton();

			if (show && _timerBar) _timerBar.gameObject.SetActive(Data.HasTurnClock);
		}

		private void BuildButton()
		{
			if (!_buttonPrefab || !_buttonRow || !LocalPlayer) return;

			_button = Instantiate(_buttonPrefab, _buttonRow);
			_button.name = $"Button_Feed_{LocalPlayer.DisplayName}";

			var label = _button.GetComponentInChildren<TextMeshProUGUI>();
			if (label) label.text = LocalPlayer.DisplayName;

			_button.OnClick += HandlePick;
		}

		private void ClearButton()
		{
			if (_button) Destroy(_button.gameObject);

			_button = null;
		}

		// The amount carries an identity rather than a size: a seat index, the same trick the wager plays
		// with the mushroom kind.
		private void HandlePick()
		{
			if (GameMode && LocalData) GameMode.SubmitActionRPC(PokerActionType.Target, LocalData.SeatIndex.Value);
		}
	}
}
