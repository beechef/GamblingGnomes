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
	// is the actual decision — and the menu and the pickers are panels of one group, so only one is ever up.
	// Where the table draws the kind, the press is the whole answer. The Items button lives in the shortcut
	// column (UI_ShortcutButtons), outside the menu, so held items can be read at any time; the picker greys each one with the reason.
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

		[Tooltip("Shown over the Fold button while an item forbids folding on a street that allows it. The button stays up, locked.")]
		[SerializeField] private GameObject _foldLock;

		[Tooltip("Shown only on a street that allows going all in.")]
		[SerializeField] private UIButton _allInButton;

		[Tooltip("In the shortcut column (UI_ShortcutButtons), not the menu (wired on UI_Poker). Shown only at a table that deals items; greyed while nothing is held.")]
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
			Data.Phase.OnValueChanged += HandlePhaseChanged;
			GameMode.OnActionRulesChanged += RefreshMenuButtons;
			GameMode.OnSeatedPlayersChanged += WatchSeatedPlayers;
			WatchSeatedPlayers();

			if (LocalPlayer.ItemInventory)
			{
				LocalPlayer.ItemInventory.OnItemsChanged += Refresh;
				LocalPlayer.ItemInventory.OnUsesChanged += RefreshMenuButtons;
			}

			if (LocalData) LocalData.OnHallucinationChanged += HandleHallucinationChanged;

			Refresh();
		}

		protected override void OnUnbind()
		{
			if (LocalData) LocalData.OnHallucinationChanged -= HandleHallucinationChanged;

			if (LocalPlayer.ItemInventory)
			{
				LocalPlayer.ItemInventory.OnUsesChanged -= RefreshMenuButtons;
				LocalPlayer.ItemInventory.OnItemsChanged -= Refresh;
			}

			UnwatchSeatedPlayers();
			GameMode.OnSeatedPlayersChanged -= WatchSeatedPlayers;
			GameMode.OnActionRulesChanged -= RefreshMenuButtons;
			Data.Phase.OnValueChanged -= HandlePhaseChanged;
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
		private void HandlePhaseChanged(PokerPhase previous, PokerPhase current) => RefreshMenuButtons();
		private void HandleHallucinationChanged(int previous, int current) => Refresh();

		// Out of the game: nothing on this bar is theirs to answer any more, items included.
		private bool IsAlive => LocalData && LocalData.IsAlive;

		private bool IsHandHelperOpen => _handHelper && _handHelper.IsOpen;

		private void Refresh()
		{
			// Resolved from the replicated stage id, never from GameMode.CurrentStage: that is written only by
			// the server's own stage machine, so on a client it is null forever.
			_stage = GameMode ? GameMode.FindStage(Data.StageId.Value.ToString()) as PokerStreetStage : null;

			// The hand board covers the same moment, so the bar steps aside while it is up; closing it lands back
			// on the menu, since the pickers were put away with everything else.
			if (IsHandHelperOpen || !IsAlive || (!IsActing && !CanReadItems))
			{
				CloseAll();
				RefreshMenuButtons();
				return;
			}

			// Off the turn only the items can be read. The menu holds nothing but the turn's answers, so it goes,
			// with a bet picker or an aim left over from the turn; an item picker the player opened stays.
			if (!IsActing)
			{
				if (_panels && !_panels.IsShowing(_itemPickerPanel)) CloseAll();
				RefreshMenuButtons();
				return;
			}

			// Whichever picker the player is on stays up; otherwise the menu.
			if (_panels && (_panels.IsShowing(_pickerPanel) || _panels.IsShowing(_itemPickerPanel) || _panels.IsShowing(_targetingPanel))) return;

			if (_panels && _panels.IsShowing(_menuPanel)) RefreshMenuButtons();
			else ShowMenu();
		}

		// Betting, folding and going all in are answers to this player's turn on a street.
		private bool IsActing => _stage != null && IsLocalTurn;

		// Held items can be read at any moment, so a player can study them before their turn comes.
		private bool CanReadItems => _itemModule && _itemPicker && LocalPlayer && LocalPlayer.ItemInventory && LocalPlayer.ItemInventory.Items.Count > 0;

		private void ShowMenu()
		{
			if (_picker) _picker.Close();
			if (_itemPicker) _itemPicker.Close();

			// Off the turn there is no menu to go back to, so stepping back from the item picker puts it all away.
			if (_panels)
			{
				if (IsActing) _panels.Show(_menuPanel);
				else _panels.HideAll();
			}

			RefreshMenuButtons();
		}

		private void RefreshMenuButtons()
		{
			if (!IsBound) return;

			var acting = IsActing && IsAlive;

			if (_betButton) _betButton.gameObject.SetActive(acting);
			// A street without folding hides the button; an item forbidding it leaves it up, locked, so the lock reads.
			var foldShown = acting && _stage.AllowsFold;
			var foldLocked = foldShown && !_stage.CanFold(LocalData);
			if (_foldButton)
			{
				_foldButton.gameObject.SetActive(foldShown);
				_foldButton.IsInteractable = !foldLocked;
			}

			if (_foldLock) _foldLock.SetActive(foldLocked);
			if (_allInButton) _allInButton.gameObject.SetActive(acting && _stage.AllowAllIn);
			if (_betLabel && acting) _betLabel.text = _stage.IsCall(LocalData) ? _callText : _betText;

			if (!_itemsButton) return;

			// Nothing is dealt before the match starts, so the waiting room shows no Items button at all.
			var hasItems = _itemModule && LocalPlayer.ItemInventory && _itemPicker && Data.Phase.Value != PokerPhase.Waiting && IsAlive;
			_itemsButton.gameObject.SetActive(hasItems);
			if (hasItems) _itemsButton.IsInteractable = AnyItemHeld();
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
		private bool AnyItemHeld()
		{
			foreach (var unit in LocalPlayer.ItemInventory.Items)
			{
				if (_itemModule.TryGetItem(unit.Type, out _)) return true;
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
			if (!_itemModule || !_itemPicker || IsHandHelperOpen || !IsAlive) return;

			// The button toggles: pressed again it steps back exactly as Escape does.
			if (_panels && _panels.IsShowing(_itemPickerPanel))
			{
				_itemPicker.Close();
				ShowMenu();
				return;
			}

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
				if (_targetingPrompt) _targetingPrompt.text = UIPokerActionNotice.Capitalize(targeting.Prompt?.ToLowerInvariant());
				return;
			}

			// Sent, or cancelled from somewhere else: either way the aiming is over and the turn is still ours. The
			// aiming panel is put away first, since Refresh keeps whichever panel is up and would leave the prompt
			// standing until the turn ended.
			EndTargeting();
			if (_panels && _panels.IsShowing(_targetingPanel)) _panels.HideAll();
			if (IsBound) Refresh();
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
