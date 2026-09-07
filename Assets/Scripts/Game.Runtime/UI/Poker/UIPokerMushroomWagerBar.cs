using Unity.Collections;
using System.Collections.Generic;
using Game.Runtime.GameMode.Poker;
using Game.Runtime.GameMode.Poker.Stages;
using Game.Runtime.UI.Button;
using Game.Runtime.UI.Progress;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Runtime.UI.Poker
{
	// Which cap to put up. One button per kind, built from the table's own mushroom database rather than
	// authored one by one: the kinds are data, so a fifth cap is an entry in an asset and not a prefab
	// edit, and a button can never offer a kind the server would refuse.
	//
	// Placeholder art: the button prefab is the project's plain one, tinted with the kind's own colour
	// and labelled with its name.
	public class UIPokerMushroomWagerBar : UIPokerView
	{
		[Header("Panel")]
		[SerializeField] private GameObject _panel;

		[Header("Buttons")]
		[Tooltip("Button prefab cloned once per mushroom kind. Instantiated under the row below.")]
		[SerializeField] private UIButton _buttonPrefab;

		[Tooltip("Row the kind buttons are laid into. A layout group, so a kind added or removed reflows.")]
		[SerializeField] private RectTransform _buttonRow;

		[Tooltip("Shown only on the wager that allows it — the second one.")]
		[SerializeField] private UIButton _foldButton;

		[Header("Turn Timer")]
		[Tooltip("Hidden outright when the stage runs no clock, rather than drawn sitting at zero.")]
		[SerializeField] private UITimerBar _timerBar;

		private readonly List<UIButton> _kindButtons = new();
		private readonly List<byte> _kindTypes = new();

		// The clock is the only thing here that changes every frame, and only while it is running.
		protected override bool WantsTick => IsLocalTurn && Data && Data.HasTurnClock;

		private void Awake()
		{
			if (_panel) _panel.SetActive(false);
		}

		protected override void OnBind()
		{
			BuildKindButtons();

			if (_foldButton) _foldButton.OnClick += HandleFold;

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

			if (_foldButton) _foldButton.OnClick -= HandleFold;

			ClearKindButtons();

			if (_panel) _panel.SetActive(false);
		}

		protected override void OnTick()
		{
			if (!_timerBar) return;

			_timerBar.SetTime(Data.TurnRemaining, Data.TurnNormalized);
		}

		private void HandleTurnChanged(ulong previous, ulong current) => Refresh();
		private void HandleStageChanged(FixedString32Bytes previous, FixedString32Bytes current) => Refresh();

		// Rebuilt on bind rather than on every refresh: the database does not change while a table runs,
		// and re-instantiating a row of buttons under the pointer is how a click lands on nothing.
		private void BuildKindButtons()
		{
			ClearKindButtons();

			var database = GameMode ? GameMode.MushroomDatabase : null;
			if (!database || !_buttonPrefab || !_buttonRow) return;

			for (var i = 0; i < database.Entries.Count; i++)
			{
				var itemType = (byte)(i + 1);
				if (!database.TryGetEntry(itemType, out var entry)) continue;

				// A kind nobody may ever wager is not drawn at all. Greying it would say "not now" about
				// something the rules say "not here" to — the Colorful cap is only ever fed to somebody.
				if (!entry.Wagerable) continue;

				var button = Instantiate(_buttonPrefab, _buttonRow);
				button.name = $"Button_Wager_{entry.DisplayName}";

				var label = button.GetComponentInChildren<TextMeshProUGUI>();
				if (label) label.text = entry.DisplayName;

				var image = button.GetComponent<Image>();
				if (image) image.color = entry.Color;

				// Captured per button rather than read back off the click: the row's order is the
				// database's order, and nothing later is allowed to depend on that staying true.
				var wagered = itemType;
				button.OnClick += () => HandleWager(wagered);

				_kindButtons.Add(button);
				_kindTypes.Add(itemType);
			}
		}

		private void ClearKindButtons()
		{
			foreach (var button in _kindButtons)
			{
				if (button) Destroy(button.gameObject);
			}

			_kindButtons.Clear();
			_kindTypes.Clear();
		}

		private void Refresh()
		{
			// Resolved from the replicated stage id, never from GameMode.ActiveStage: that is written only by
			// the server's own stage machine, so on a client it is null forever and the bar never appears —
			// right on the host, missing everywhere else.
			var stage = GameMode ? GameMode.FindStage(Data.StageId.Value.ToString()) as PokerMushroomWagerStage : null;

			// An overlay hands out turns of its own, and whoever is on that clock is being asked something
			// else entirely.
			var show = stage != null && IsLocalTurn && Data.OverlayStageId.Value.IsEmpty;

			if (_panel) _panel.SetActive(show);
			if (!show) return;

			// What the rules forbid is hidden; what this player cannot do right now is greyed. Folding is
			// not on offer at the first wager at all, so it goes rather than dimming.
			if (_foldButton) _foldButton.gameObject.SetActive(stage.AllowFold);

			for (var i = 0; i < _kindButtons.Count; i++)
			{
				var button = _kindButtons[i];
				if (button) button.IsInteractable = stage.IsWagerable(_kindTypes[i]);
			}

			if (_timerBar) _timerBar.gameObject.SetActive(Data.HasTurnClock);
		}

		private void HandleWager(byte itemType) => Submit(PokerActionType.Wager, itemType);
		private void HandleFold() => Submit(PokerActionType.Fold, 0);

		private void Submit(PokerActionType action, int amount)
		{
			if (GameMode) GameMode.SubmitActionRPC(action, amount);
		}
	}
}
