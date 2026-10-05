using System;
using System.Collections.Generic;
using Game.Runtime.GameMode.Poker;
using Game.Runtime.GameMode.Poker.Items;
using Game.Runtime.GameMode.Poker.Player;
using Game.Runtime.UI.Selection;
using Unity.Collections;
using UnityEngine;

namespace Game.Runtime.UI.Poker
{
	// Which item to play, dropped up from the Items button: a column of item icons, the marked one showing its
	// tooltip. One entry per card held; one that cannot be played right now is greyed with the reason in its
	// tooltip. Clicking (or submitting) an entry plays it. The answer comes from PokerItemModule.GetAvailability,
	// the call the server refuses with.
	//
	// Opened and closed by the bet bar, which owns the turn. Escape closes it through UIEscapeStack.
	public class UIPokerItemPicker : MonoBehaviour
	{
		[Header("Entries")]
		[Tooltip("One held item, instantiated per card under the column (UI_PokerItemIcon).")]
		[SerializeField] private UIPokerItemEntry _entryPrefab;

		[Tooltip("Auto-layout column the entries are laid out in, growing up from the Items button.")]
		[SerializeField] private RectTransform _entryRow;

		[SerializeField] private UISelectionGroup _selection;

		private readonly List<UIPokerItemEntry> _entries = new();
		private readonly List<UISelectionItem> _selectionItems = new();

		private PokerGameMode _gameMode;
		private PokerPlayer _player;
		private PokerItemModule _module;
		private Action _back;
		private Action<PokerItem> _chosen;

		private void OnEnable()
		{
			if (!_selection) return;

			_selection.OnSelectionChanged += HandleSelectionChanged;
			_selection.OnSubmitted += HandleSubmitted;
		}

		private void OnDisable()
		{
			if (!_selection) return;

			_selection.OnSubmitted -= HandleSubmitted;
			_selection.OnSelectionChanged -= HandleSelectionChanged;
		}

		private void OnDestroy() => Close();

		public void Open(PokerGameMode gameMode, PokerPlayer player, PokerItemModule module, Action back, Action<PokerItem> chosen)
		{
			Close();

			_gameMode = gameMode;
			_player = player;
			_module = module;
			_back = back;
			_chosen = chosen;

			if (_player && _player.ItemInventory)
			{
				_player.ItemInventory.OnItemsChanged += Rebuild;
				_player.ItemInventory.OnUsesChanged += Rebuild;
			}

			// Whose turn it is and which stage runs decide what can be played, and the picker can stay open across both.
			if (_gameMode)
			{
				_gameMode.OnActionRulesChanged += Rebuild;
				_gameMode.Data.CurrentTurnClientId.OnValueChanged += HandleTurnChanged;
				_gameMode.Data.StageId.OnValueChanged += HandleStageChanged;
			}

			Rebuild();
			UIEscapeStack.Push(HandleEscape);
		}

		public void Close()
		{
			UIEscapeStack.Remove(HandleEscape);

			if (_gameMode)
			{
				_gameMode.Data.StageId.OnValueChanged -= HandleStageChanged;
				_gameMode.Data.CurrentTurnClientId.OnValueChanged -= HandleTurnChanged;
				_gameMode.OnActionRulesChanged -= Rebuild;
			}

			if (_player && _player.ItemInventory)
			{
				_player.ItemInventory.OnUsesChanged -= Rebuild;
				_player.ItemInventory.OnItemsChanged -= Rebuild;
			}

			_back = null;
			_chosen = null;
			_module = null;
			_player = null;
			_gameMode = null;
		}

		// Views already made are re-bound rather than rebuilt, so the column never flashes under the pointer.
		private void Rebuild()
		{
			var inventory = _player ? _player.ItemInventory : null;
			if (!_module || !inventory || !_entryPrefab || !_entryRow) return;

			var used = 0;
			_selectionItems.Clear();

			foreach (var unit in inventory.Items)
			{
				if (!_module.TryGetItem(unit.Type, out var item)) continue;

				if (used == _entries.Count) _entries.Add(Instantiate(_entryPrefab, _entryRow));

				// Every item held is drawn, even one the rules forbid right now: an item missing from the column
				// reads as an item lost. What cannot be played is dimmed, and its tooltip says why.
				var entry = _entries[used++];
				entry.gameObject.SetActive(true);
				entry.Bind(item, _module.GetAvailability(_player, unit.Type));

				if (entry.SelectionItem) _selectionItems.Add(entry.SelectionItem);
			}

			for (var i = used; i < _entries.Count; i++) _entries[i].gameObject.SetActive(false);

			if (_selection) _selection.SetItems(_selectionItems);

			ShowTooltipOf(_selection ? _selection.Selected : null);
		}

		private void HandleTurnChanged(ulong previous, ulong current) => Rebuild();
		private void HandleStageChanged(FixedString32Bytes previous, FixedString32Bytes current) => Rebuild();

		private void HandleSelectionChanged(UISelectionItem item) => ShowTooltipOf(item);

		private void ShowTooltipOf(UISelectionItem selected)
		{
			foreach (var entry in _entries) entry.SetTooltipShown(selected && entry.SelectionItem == selected);
		}

		private void HandleSubmitted(UISelectionItem selected)
		{
			var entry = EntryOf(selected);
			if (!entry || !entry.Item || !_module || !_gameMode) return;
			if (!_module.GetAvailability(_player, entry.Item.Type).IsUsable) return;

			// Handed to the bar, which aims it if it needs aiming and sends it; the picker's part is over.
			var chosen = _chosen;
			var item = entry.Item;
			Close();
			chosen?.Invoke(item);
		}

		private UIPokerItemEntry EntryOf(UISelectionItem item)
		{
			if (!item) return null;

			foreach (var entry in _entries)
			{
				if (entry.SelectionItem == item) return entry;
			}

			return null;
		}

		private void HandleEscape()
		{
			var back = _back;
			Close();
			back?.Invoke();
		}
	}
}
