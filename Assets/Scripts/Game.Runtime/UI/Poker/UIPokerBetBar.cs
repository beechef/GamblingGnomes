using System.Collections.Generic;
using Unity.Collections;
using Game.Runtime.GameMode.Poker;
using Game.Runtime.GameMode.Poker.Items;
using Game.Runtime.GameMode.Poker.Player;
using Game.Runtime.GameMode.Poker.Stages;
using Game.Runtime.UI.Button;
using TMPro;
using UnityEngine;

namespace Game.Runtime.UI.Poker
{
	// What the player can do on their own betting turn: bet, fold, go all in, or play an item. Where the
	// player picks the kind, betting does not put a cap up by itself — it opens the picker, because the kind
	// is the actual decision — and the menu and the pickers are panels of one group, so only one is ever up
	// and none is up off the turn. Where the table draws the kind, the press is the whole answer.
	//
	// This component stays on an object that is always active and only switches the panels, or it would
	// switch itself off with the menu and never hear the turn come round again.
	public class UIPokerBetBar : UIPokerView
	{
		[Header("Panels")]
		[SerializeField] private UIPanelStateGroup _panels;
		[SerializeField] private GameObject _menuPanel;
		[SerializeField] private GameObject _pickerPanel;
		[SerializeField] private UIPokerBetPicker _picker;

		[Tooltip("Optional. Opened by the Items button; a table without PokerItemModule never shows it.")]
		[SerializeField] private GameObject _itemPickerPanel;
		[SerializeField] private UIPokerItemPicker _itemPicker;

		[Tooltip("Up while an item is being aimed: says what to point at. Escape puts the item back and returns to the menu.")]
		[SerializeField] private GameObject _targetingPanel;
		[SerializeField] private TMP_Text _targetingPrompt;

		[Header("Menu")]
		[SerializeField] private UIButton _betButton;

		[Tooltip("The Bet button's label. Reads the bet text for whoever opens the street and the call text once somebody still in the hand has bet on it.")]
		[SerializeField] private TMP_Text _betLabel;

		[SerializeField] private string _betText = "BET";
		[SerializeField] private string _callText = "CALL";

		[Tooltip("Shown only where folding is allowed — by the street and by whatever items are in play. What the rules forbid is hidden, not greyed.")]
		[SerializeField] private UIButton _foldButton;

		[Tooltip("Shown only on a street that allows going all in.")]
		[SerializeField] private UIButton _allInButton;

		[Tooltip("Shown only at a table that deals items; greyed while nothing held can be played.")]
		[SerializeField] private UIButton _itemsButton;

		[Header("Overlays")]
		[Tooltip("Optional. While the hand board is open the bar steps aside, and the turn comes back to the menu when it closes.")]
		[SerializeField] private UIPokerHandHelper _handHelper;

		private PokerStreetStage _stage;
		private PokerItemModule _itemModule;

		// Everybody seated, because whether the button reads Bet or Call hangs on what they did, and their
		// bet can reach this client after the turn that follows it.
		private readonly List<PokerPlayerData> _watched = new();

		private void Awake()
		{
			if (_panels) _panels.HideAll();
		}

		protected override void OnBind()
		{
			_itemModule = GameMode.FindModule<PokerItemModule>();

			if (_betButton) _betButton.OnClick += HandleBet;
			if (_foldButton) _foldButton.OnClick += HandleFold;
			if (_allInButton) _allInButton.OnClick += HandleAllIn;
			if (_itemsButton) _itemsButton.OnClick += HandleItems;
			if (_handHelper) _handHelper.OnOpenChanged += HandleHandHelperOpenChanged;

			Data.CurrentTurnClientId.OnValueChanged += HandleTurnChanged;
			Data.StageId.OnValueChanged += HandleStageChanged;
			GameMode.OnActionRulesChanged += RefreshMenuButtons;
			GameMode.OnSeatedPlayersChanged += WatchSeatedPlayers;
			WatchSeatedPlayers();

			if (LocalPlayer.ItemInventory)
			{
				LocalPlayer.ItemInventory.OnItemsChanged += RefreshMenuButtons;
				LocalPlayer.ItemInventory.OnUsesChanged += RefreshMenuButtons;
			}

			Refresh();
		}

		protected override void OnUnbind()
		{
			if (LocalPlayer.ItemInventory)
			{
				LocalPlayer.ItemInventory.OnUsesChanged -= RefreshMenuButtons;
				LocalPlayer.ItemInventory.OnItemsChanged -= RefreshMenuButtons;
			}

			UnwatchSeatedPlayers();
			GameMode.OnSeatedPlayersChanged -= WatchSeatedPlayers;
			GameMode.OnActionRulesChanged -= RefreshMenuButtons;
			if (_handHelper) _handHelper.OnOpenChanged -= HandleHandHelperOpenChanged;
			Data.StageId.OnValueChanged -= HandleStageChanged;
			Data.CurrentTurnClientId.OnValueChanged -= HandleTurnChanged;

			if (_itemsButton) _itemsButton.OnClick -= HandleItems;
			if (_allInButton) _allInButton.OnClick -= HandleAllIn;
			if (_foldButton) _foldButton.OnClick -= HandleFold;
			if (_betButton) _betButton.OnClick -= HandleBet;

			CloseAll();

			_itemModule = null;
		}

		private void HandleTurnChanged(ulong previous, ulong current) => Refresh();
		private void HandleStageChanged(FixedString32Bytes previous, FixedString32Bytes current) => Refresh();
		private void HandleHandHelperOpenChanged(bool open) => Refresh();

		private bool IsHandHelperOpen => _handHelper && _handHelper.IsOpen;

		private void Refresh()
		{
			// Resolved from the replicated stage id, never from GameMode.CurrentStage: that is written only by
			// the server's own stage machine, so on a client it is null forever.
			_stage = GameMode ? GameMode.FindStage(Data.StageId.Value.ToString()) as PokerStreetStage : null;

			// The hand board covers the same moment, so the bar steps aside while it is up; closing it lands back
			// on the menu, since the pickers were put away with everything else.
			if (_stage == null || !IsLocalTurn || IsHandHelperOpen)
			{
				CloseAll();
				return;
			}

			// A turn that is still ours keeps whichever panel the player is on; only a fresh turn opens the menu.
			if (_panels && !_panels.IsShowing(_pickerPanel) && !_panels.IsShowing(_itemPickerPanel) && !_panels.IsShowing(_targetingPanel)) ShowMenu();
		}

		private void ShowMenu()
		{
			if (_picker) _picker.Close();
			if (_itemPicker) _itemPicker.Close();
			if (_panels) _panels.Show(_menuPanel);

			RefreshMenuButtons();
		}

		private void RefreshMenuButtons()
		{
			if (_stage == null || !IsBound) return;

			if (_foldButton) _foldButton.gameObject.SetActive(_stage.CanFold(LocalData));
			if (_allInButton) _allInButton.gameObject.SetActive(_stage.AllowAllIn);
			if (_betLabel) _betLabel.text = _stage.IsCall(LocalData) ? _callText : _betText;

			if (!_itemsButton) return;

			var hasItems = _itemModule && LocalPlayer.ItemInventory && _itemPicker;
			_itemsButton.gameObject.SetActive(hasItems);
			if (hasItems) _itemsButton.IsInteractable = AnyItemShown();
		}

		private void WatchSeatedPlayers()
		{
			UnwatchSeatedPlayers();

			foreach (var player in GameMode.SeatedPlayers)
			{
				if (!player || !player.Data) continue;

				player.Data.OnStateChanged += RefreshMenuButtons;
				_watched.Add(player.Data);
			}
		}

		private void UnwatchSeatedPlayers()
		{
			foreach (var data in _watched)
			{
				if (data) data.OnStateChanged -= RefreshMenuButtons;
			}

			_watched.Clear();
		}

		// An item that cannot be played yet still opens the picker, where its entry says why; a dimmed button would hide the reason.
		private bool AnyItemShown()
		{
			foreach (var unit in LocalPlayer.ItemInventory.Items)
			{
				if (_itemModule.GetAvailability(LocalPlayer, unit.Type).IsShown) return true;
			}

			return false;
		}

		private void HandleBet()
		{
			if (_stage == null || !IsLocalTurn || IsHandHelperOpen) return;

			if (!_stage.PicksKind)
			{
				GameMode.SubmitActionRPC(PokerActionType.Bet, 0);
				return;
			}

			if (_panels) _panels.Show(_pickerPanel);
			if (_picker) _picker.Open(GameMode, LocalPlayer, _stage, ShowMenu);
		}

		private void HandleFold()
		{
			if (_stage == null || !_stage.CanFold(LocalData) || !IsLocalTurn) return;

			GameMode.SubmitActionRPC(PokerActionType.Fold, 0);
		}

		private void HandleAllIn()
		{
			if (_stage == null || !_stage.AllowAllIn || !IsLocalTurn || IsHandHelperOpen) return;

			GameMode.SubmitActionRPC(PokerActionType.AllIn, 0);
		}

		private void HandleItems()
		{
			if (_stage == null || !_itemModule || !_itemPicker || !IsLocalTurn || IsHandHelperOpen) return;

			if (_panels) _panels.Show(_itemPickerPanel);
			_itemPicker.Open(GameMode, LocalPlayer, _itemModule, ShowMenu, HandleItemChosen);
		}

		// An item that points at nothing is sent at once; one that does is aimed first, with the menu put away
		// so the pointer is free to reach the table.
		private void HandleItemChosen(PokerItem item)
		{
			var targeting = LocalPlayer ? LocalPlayer.ItemTargeting : null;
			if (!targeting)
			{
				ShowMenu();
				return;
			}

			targeting.OnTargetingChanged -= HandleTargetingChanged;
			targeting.OnTargetingChanged += HandleTargetingChanged;
			targeting.BeginUse(item);

			if (!targeting.IsTargeting)
			{
				targeting.OnTargetingChanged -= HandleTargetingChanged;
				ShowMenu();
				return;
			}

			if (_panels) _panels.Show(_targetingPanel);
			UIEscapeStack.Push(HandleTargetingEscape);
			HandleTargetingChanged();
		}

		private void HandleTargetingChanged()
		{
			var targeting = LocalPlayer ? LocalPlayer.ItemTargeting : null;

			if (targeting && targeting.IsTargeting)
			{
				if (_targetingPrompt) _targetingPrompt.text = targeting.Prompt;
				return;
			}

			// Sent, or cancelled from somewhere else: either way the aiming is over and the turn is still ours.
			EndTargeting();
			if (IsBound && IsLocalTurn && _stage != null && !IsHandHelperOpen) ShowMenu();
		}

		private void HandleTargetingEscape()
		{
			var targeting = LocalPlayer ? LocalPlayer.ItemTargeting : null;
			if (targeting) targeting.Cancel();
		}

		private void EndTargeting()
		{
			UIEscapeStack.Remove(HandleTargetingEscape);

			var targeting = LocalPlayer ? LocalPlayer.ItemTargeting : null;
			if (targeting) targeting.OnTargetingChanged -= HandleTargetingChanged;
		}

		private void CloseAll()
		{
			if (_picker) _picker.Close();
			if (_itemPicker) _itemPicker.Close();

			var targeting = LocalPlayer ? LocalPlayer.ItemTargeting : null;
			EndTargeting();
			if (targeting) targeting.Cancel();

			if (_panels) _panels.HideAll();
		}
	}
}
