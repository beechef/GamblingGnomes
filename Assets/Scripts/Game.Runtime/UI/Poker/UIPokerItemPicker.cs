using System;
using System.Collections.Generic;
using Game.Runtime.GameMode.Poker;
using Game.Runtime.GameMode.Poker.Items;
using Game.Runtime.GameMode.Poker.Player;
using Game.Runtime.UI.Button;
using Game.Runtime.UI.Selection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Runtime.UI.Poker
{
	// Which item to play, opened from the bet bar's Items button. One entry per card held; an item the rules
	// forbid right now is not drawn, one that merely cannot be played yet is greyed with the reason under it.
	// Both answers come from PokerItemModule.GetAvailability, the call the server refuses with.
	//
	// Opened and closed by the bet bar, which owns the turn. Escape steps back to the menu through UIEscapeStack.
	public class UIPokerItemPicker : MonoBehaviour
	{
		[Header("Entries")]
		[Tooltip("One held item, instantiated per card under the row (UI_PokerItemEntry).")]
		[SerializeField] private UIPokerItemEntry _entryPrefab;

		[Tooltip("Auto-layout row the entries are laid out in.")]
		[SerializeField] private RectTransform _entryRow;

		[SerializeField] private UISelectionGroup _selection;

		[Tooltip("Plays whatever item is marked. Greyed while the marked item cannot be played.")]
		[SerializeField] private UIButton _chooseButton;

		[Header("Details")]
		[SerializeField] private TMP_Text _nameLabel;
		[SerializeField] private TMP_Text _descriptionLabel;

		[Tooltip("Why the marked item cannot be played right now. Empty when it can.")]
		[SerializeField] private TMP_Text _reasonLabel;

		[Tooltip("Faded in while an item is marked, so the panel does not reflow when nothing is.")]
		[SerializeField] private CanvasGroup _detailsGroup;

		private readonly List<UIPokerItemEntry> _entries = new();
		private readonly List<UISelectionItem> _selectionItems = new();

		private PokerGameMode _gameMode;
		private PokerPlayer _player;
		private PokerItemModule _module;
		private Action _back;
		private Action<PokerItem> _chosen;

		private void Awake()
		{
			SetDetailsVisible(false);
		}

		private void OnEnable()
		{
			if (_selection)
			{
				_selection.OnSelectionChanged += HandleSelectionChanged;
				_selection.OnSubmitted += HandleSubmitted;
			}

			if (_chooseButton) _chooseButton.OnClick += HandleChoose;

			RebuildDetailsLayout();
		}

		private void OnDisable()
		{
			if (_chooseButton) _chooseButton.OnClick -= HandleChoose;

			if (_selection)
			{
				_selection.OnSubmitted -= HandleSubmitted;
				_selection.OnSelectionChanged -= HandleSelectionChanged;
			}
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

			if (_gameMode) _gameMode.OnActionRulesChanged += Rebuild;

			Rebuild();
			UIEscapeStack.Push(HandleEscape);
		}

		public void Close()
		{
			UIEscapeStack.Remove(HandleEscape);

			if (_gameMode) _gameMode.OnActionRulesChanged -= Rebuild;

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

			SetDetailsVisible(false);
		}

		// Views already made are re-bound rather than rebuilt, so the row never flashes under the pointer.
		private void Rebuild()
		{
			var inventory = _player ? _player.ItemInventory : null;
			if (!_module || !inventory || !_entryPrefab || !_entryRow) return;

			var used = 0;
			_selectionItems.Clear();

			foreach (var unit in inventory.Items)
			{
				if (!_module.TryGetItem(unit.Type, out var item)) continue;

				var availability = _module.GetAvailability(_player, unit.Type);
				if (!availability.IsShown) continue;

				if (used == _entries.Count) _entries.Add(Instantiate(_entryPrefab, _entryRow));

				var entry = _entries[used++];
				entry.gameObject.SetActive(true);
				entry.Bind(item);
				entry.SetUsable(availability.IsUsable);

				if (entry.SelectionItem) _selectionItems.Add(entry.SelectionItem);
			}

			for (var i = used; i < _entries.Count; i++) _entries[i].gameObject.SetActive(false);

			if (_selection) _selection.SetItems(_selectionItems);

			RefreshDetails(_selection ? _selection.Selected : null);
		}

		private void HandleSelectionChanged(UISelectionItem item) => RefreshDetails(item);

		private void RefreshDetails(UISelectionItem selected)
		{
			var entry = EntryOf(selected);

			if (!entry || !entry.Item || !_module)
			{
				if (_chooseButton) _chooseButton.IsInteractable = false;
				SetDetailsVisible(false);
				return;
			}

			var availability = _module.GetAvailability(_player, entry.Item.Type);

			if (_chooseButton) _chooseButton.IsInteractable = availability.IsUsable;
			if (_nameLabel) _nameLabel.text = entry.Item.DisplayName;
			if (_descriptionLabel) _descriptionLabel.text = entry.Item.Description;
			if (_reasonLabel) _reasonLabel.text = availability.IsUsable ? string.Empty : availability.BlockReason;

			SetDetailsVisible(true);
			RebuildDetailsLayout();
		}

		private void HandleChoose()
		{
			if (_selection) HandleSubmitted(_selection.Selected);
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

		// The description wraps, so its height is only right once laid out at the column's width; the first
		// open sets the text before that pass and the reason overlaps it until something else rebuilds.
		private void RebuildDetailsLayout()
		{
			if (_detailsGroup && _detailsGroup.gameObject.activeInHierarchy)
				LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)_detailsGroup.transform);
		}

		private void SetDetailsVisible(bool visible)
		{
			if (_detailsGroup) _detailsGroup.alpha = visible ? 1f : 0f;
		}
	}
}
