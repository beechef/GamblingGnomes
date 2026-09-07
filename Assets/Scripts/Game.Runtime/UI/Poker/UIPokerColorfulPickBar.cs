using System.Collections.Generic;
using Game.Runtime.GameMode.Poker;
using Game.Runtime.GameMode.Poker.Stages;
using Game.Runtime.UI.Button;
using Game.Runtime.UI.Progress;
using TMPro;
using Unity.Collections;
using UnityEngine;

namespace Game.Runtime.UI.Poker
{
	// Who eats the Colorful cap. The winner is handed the table and cannot decline, so this is a row of
	// names and nothing else — there is no cancel, and the stage waits for an answer rather than running
	// a clock. Without it the round simply stops here: the turn is on the winner, and no client has any
	// way to send the Target action the stage is waiting for.
	//
	// Placeholder art: the project's plain button, labelled with the player's name.
	public class UIPokerColorfulPickBar : UIPokerView
	{
		[Header("Panel")]
		[SerializeField] private GameObject _panel;

		[Header("Buttons")]
		[Tooltip("Button prefab cloned once per player who can still be fed.")]
		[SerializeField] private UIButton _buttonPrefab;

		[Tooltip("Row the name buttons are laid into. A layout group, so a seat added or removed reflows.")]
		[SerializeField] private RectTransform _buttonRow;

		[Header("Turn Timer")]
		[Tooltip("Hidden outright when the stage runs no clock, rather than drawn sitting at zero.")]
		[SerializeField] private UITimerBar _timerBar;

		private readonly List<UIButton> _buttons = new();

		private bool _shown;

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

			ClearButtons();
			_shown = false;

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
			var show = stage != null && IsLocalTurn && Data.OverlayStageId.Value.IsEmpty;

			if (_panel) _panel.SetActive(show);

			// Built once as the bar comes up rather than on every refresh: re-instantiating a row of
			// buttons under the pointer is how a click lands on nothing.
			if (show && !_shown) BuildButtons();
			else if (!show && _shown) ClearButtons();

			_shown = show;
			if (!show) return;

			if (_timerBar) _timerBar.gameObject.SetActive(Data.HasTurnClock);
		}

		private void BuildButtons()
		{
			ClearButtons();

			if (!_buttonPrefab || !_buttonRow || !GameMode) return;

			foreach (var player in GameMode.SeatedPlayers)
			{
				// Somebody already at the ceiling cannot be chosen — feeding them is a move with no
				// consequence at all, and the server refuses it. Naming yourself is allowed on purpose.
				if (!player || !player.Data || !player.Data.IsSeated || !player.Data.IsAlive) continue;

				var button = Instantiate(_buttonPrefab, _buttonRow);
				button.name = $"Button_Feed_{player.DisplayName}";

				var label = button.GetComponentInChildren<TextMeshProUGUI>();
				if (label) label.text = player.DisplayName;

				var seat = player.Data.SeatIndex.Value;
				button.OnClick += () => HandlePick(seat);

				_buttons.Add(button);
			}
		}

		private void ClearButtons()
		{
			foreach (var button in _buttons)
			{
				if (button) Destroy(button.gameObject);
			}

			_buttons.Clear();
		}

		// The amount carries an identity rather than a size: a seat index, the same trick the wager plays
		// with the mushroom kind.
		private void HandlePick(int seatIndex)
		{
			if (GameMode) GameMode.SubmitActionRPC(PokerActionType.Target, seatIndex);
		}
	}
}
