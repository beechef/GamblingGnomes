using System.Collections.Generic;
using DG.Tweening;
using Unity.Collections;
using Game.Runtime.GameMode.Poker;
using Game.Runtime.GameMode.Poker.Items;
using Game.Runtime.GameMode.Poker.Player;
using Game.Runtime.GameMode.Poker.Stages;
using Game.Runtime.UI.Button;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Game.Runtime.UI.Poker
{
	// What the player can do on their own betting turn: bet, fold, go all in, or play an item. Where the
	// player picks the kind, betting does not put a cap up by itself — it opens the picker, because the kind
	// is the actual decision — and the menu and the pickers are panels of one group, so only one is ever up.
	// Where the table draws the kind, the press is the whole answer. The Items button sits above the menu, outside
	// it, so held items can be read at any time; its picker drops up beside the menu in a group of its own, and
	// greys each item with the reason.
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

		[Tooltip("Optional. Opened by the Items button; a table without PokerItemModule never shows it. The only panel of Item Panels, so it opens beside the menu rather than in its place.")]
		[SerializeField] private GameObject _itemPickerPanel;

		[SerializeField] private UIPanelStateGroup _itemPanels;
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

		[Tooltip("Drawn across the Fold label while an item forbids folding on a street that allows it. The button stays up, undimmed, and refuses the press.")]
		[FormerlySerializedAs("_foldLock")]
		[SerializeField] private GameObject _foldCrossline;

		[Tooltip("The Fold button's icon. Wears Fold Locked Icon while folding is forbidden, its authored sprite otherwise.")]
		[SerializeField] private Image _foldIcon;

		[SerializeField] private Sprite _foldLockedIcon;

		[Tooltip("Seconds the Fold button shakes and pulses when folding becomes forbidden while it is up, so the lock is noticed.")]
		[Min(0f)]
		[SerializeField] private float _foldLockAlertDuration = 0.5f;

		[Tooltip("How much bigger the Fold button punches at the lock, as a share of its size.")]
		[SerializeField] private float _foldLockPunch = 0.25f;

		[Tooltip("Degrees the Fold button shakes either way at the lock.")]
		[SerializeField] private float _foldLockShakeAngle = 12f;

		[Tooltip("Shown only on a street that allows going all in.")]
		[SerializeField] private UIButton _allInButton;

		[Tooltip("Above the menu, not in it. Shown only at a table that deals items; greyed while nothing is held.")]
		[SerializeField] private UIButton _itemsButton;

		[Header("Overlays")]
		[Tooltip("Optional. While the hand board is open the bar steps aside, and the turn comes back to the menu when it closes.")]
		[SerializeField] private UIPokerHandHelper _handHelper;

		private PokerStreetStage _stage;
		private PokerItemModule _itemModule;

		// Everybody seated, because whether the button reads Bet or Call hangs on what they did, and their
		// bet can reach this client after the turn that follows it.
		private readonly List<PokerPlayerData> _watched = new();

		private Sprite _foldIconSprite;
		private Tween _foldLockAlert;
		private bool _wasFoldLocked;

		private void Awake()
		{
			if (_panels) _panels.HideAll();
			if (_itemPanels) _itemPanels.HideAll();
			if (_foldIcon) _foldIconSprite = _foldIcon.sprite;
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

			_foldLockAlert?.Kill(true);
			_wasFoldLocked = false;
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
				CloseTurn();
				RefreshMenuButtons();
				return;
			}

			// Whichever picker the player is on stays up; otherwise the menu.
			if (_panels && (_panels.IsShowing(_pickerPanel) || _panels.IsShowing(_targetingPanel))) return;

			if (_panels && _panels.IsShowing(_menuPanel)) RefreshMenuButtons();
			else ShowMenu();
		}

		// Betting, folding and going all in are answers to this player's turn on a street.
		private bool IsActing => _stage != null && IsLocalTurn;

		// Held items can be read at any moment, so a player can study them before their turn comes.
		private bool CanReadItems => _itemModule && _itemPicker && LocalPlayer && LocalPlayer.ItemInventory && LocalPlayer.ItemInventory.Items.Count > 0;

		private bool IsItemPickerOpen => _itemPanels && _itemPanels.IsShowing(_itemPickerPanel);

		private void ShowMenu()
		{
			if (_picker) _picker.Close();

			// Off the turn there is no menu to go back to, so stepping back puts it all away.
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
			if (_foldButton) _foldButton.gameObject.SetActive(foldShown);
			if (_foldCrossline) _foldCrossline.SetActive(foldLocked);
			if (_foldIcon && _foldLockedIcon) _foldIcon.sprite = foldLocked ? _foldLockedIcon : _foldIconSprite;
			if (foldLocked && !_wasFoldLocked) PlayFoldLockAlert();
			_wasFoldLocked = foldLocked;
			if (_allInButton) _allInButton.gameObject.SetActive(acting && _stage.AllowAllIn);
			if (_betLabel && acting) _betLabel.text = _stage.IsCall(LocalData) ? _callText : _betText;

			if (!_itemsButton) return;

			// Nothing is dealt before the match starts, so the waiting room shows no Items button at all.
			var hasItems = _itemModule && LocalPlayer.ItemInventory && _itemPicker && Data.Phase.Value != PokerPhase.Waiting && IsAlive;
			_itemsButton.gameObject.SetActive(hasItems);
			if (hasItems) _itemsButton.IsInteractable = AnyItemHeld();
		}

		// The root turns and scales; no layout places either, and the button's own visuals move its Content.
		private void PlayFoldLockAlert()
		{
			if (!_foldButton || _foldLockAlertDuration <= 0f) return;

			var button = _foldButton.transform;
			_foldLockAlert?.Kill(true);
			_foldLockAlert = DOTween.Sequence()
				.Join(button.DOPunchScale(Vector3.one * _foldLockPunch, _foldLockAlertDuration, 4, 0.5f))
				.Join(button.DOShakeRotation(_foldLockAlertDuration, new Vector3(0f, 0f, _foldLockShakeAngle), 20, 0f))
				.SetUpdate(true)
				.SetLink(_foldButton.gameObject);
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

			CloseItemPicker();

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
			if (IsItemPickerOpen)
			{
				CloseItemPicker();
				return;
			}

			// The bet picker answers the same turn, so it steps back to the menu while the items are open.
			if (_panels && _panels.IsShowing(_pickerPanel)) ShowMenu();

			if (_itemPanels) _itemPanels.Show(_itemPickerPanel);
			_itemPicker.Open(GameMode, LocalPlayer, _itemModule, CloseItemPicker, HandleItemChosen);
		}

		// An item that points at nothing is sent at once; one that does is aimed first, with the menu put away
		// so the pointer is free to reach the table.
		private void HandleItemChosen(PokerItem item)
		{
			if (_itemPanels) _itemPanels.HideAll();

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

		private void CloseItemPicker()
		{
			if (_itemPicker) _itemPicker.Close();
			if (_itemPanels) _itemPanels.HideAll();
		}

		// Everything that answers the turn: the menu, the bet picker and an item being aimed.
		private void CloseTurn()
		{
			if (_picker) _picker.Close();

			var targeting = LocalPlayer ? LocalPlayer.ItemTargeting : null;
			EndTargeting();
			if (targeting) targeting.Cancel();

			if (_panels) _panels.HideAll();
		}

		private void CloseAll()
		{
			CloseItemPicker();
			CloseTurn();
		}
	}
}
