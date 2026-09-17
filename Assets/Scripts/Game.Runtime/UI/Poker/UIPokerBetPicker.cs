using System;
using System.Collections.Generic;
using Game.Runtime.GameMode.Poker;
using Game.Runtime.GameMode.Poker.Items;
using Game.Runtime.GameMode.Poker.Player;
using Game.Runtime.GameMode.Poker.Stages;
using Game.Runtime.UI.Button;
using Game.Runtime.UI.Selection;
using TMPro;
using UnityEngine;

namespace Game.Runtime.UI.Poker
{
	// Which mushroom to put up, asked after the player has said they are betting. One button per kind that
	// may be wagered, built from the table's database so a new kind is a row in an asset; the number above
	// is what the kind currently marked would cost *this* player, asked of the kind's own effect — the same
	// call the eating beat prices it with, so the picker can never quote a different price from the one
	// charged.
	//
	// Marking and choosing are the selection group's: a mouse marks what it hovers and clicks, a pad walks
	// the row with the d-pad or stick and submits. Opened and closed by the wager bar, which owns the turn.
	// Escape (and the pad's pause) steps back to the menu through UIEscapeStack rather than a key of its own.
	public class UIPokerBetPicker : MonoBehaviour
	{
		[Header("Kinds")]
		[Tooltip("One kind, instantiated per wagerable entry under the row (UI_PokerBetKind).")]
		[SerializeField] private UIPokerBetKindButton _kindPrefab;

		[Tooltip("Auto-layout row the kinds are laid out in.")]
		[SerializeField] private RectTransform _kindRow;

		[Tooltip("Follows focus across the kinds, for the mouse and the pad alike.")]
		[SerializeField] private UISelectionGroup _selection;

		[Tooltip("Names the confirm key and confirms whatever kind is marked. Greyed while nothing is marked, so it doubles as the sign that a choice is ready.")]
		[SerializeField] private UIButton _chooseButton;

		[Header("Cost")]
		[Tooltip("Faded in while a kind is marked, so the row does not reflow when nothing is.")]
		[SerializeField] private CanvasGroup _costGroup;

		[SerializeField] private TMP_Text _costLabel;

		private readonly List<UIPokerBetKindButton> _kinds = new();
		private readonly List<UISelectionItem> _selectionItems = new();

		private PokerGameMode _gameMode;
		private PokerPlayer _player;
		private PokerItemWagerStage _stage;
		private Action _back;

		private void Awake()
		{
			SetCostVisible(false);
		}

		private void OnEnable()
		{
			if (_selection)
			{
				_selection.OnSelectionChanged += HandleSelectionChanged;
				_selection.OnSubmitted += HandleSubmitted;
			}

			if (_chooseButton) _chooseButton.OnClick += HandleChoose;
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

		public void Open(PokerGameMode gameMode, PokerPlayer player, PokerItemWagerStage stage, Action back)
		{
			_gameMode = gameMode;
			_player = player;
			_stage = stage;
			_back = back;

			BuildKinds();
			RefreshInteractable();
			RefreshCost(_selection ? _selection.Selected : null);

			UIEscapeStack.Push(HandleEscape);
		}

		public void Close()
		{
			UIEscapeStack.Remove(HandleEscape);

			_back = null;
			_stage = null;
			_player = null;
			_gameMode = null;

			SetCostVisible(false);
		}

		// Views already made are re-bound rather than rebuilt, so reopening the picker never draws a
		// fresh row under the pointer.
		private void BuildKinds()
		{
			var database = _gameMode ? _gameMode.ItemDatabase : null;
			if (!database || !_kindPrefab || !_kindRow) return;

			var used = 0;
			_selectionItems.Clear();

			foreach (var entry in database.Entries)
			{
				// A kind the rules never let anybody wager is not drawn at all.
				if (entry == null || !entry.Wagerable) continue;

				if (used == _kinds.Count) _kinds.Add(Instantiate(_kindPrefab, _kindRow));

				var kind = _kinds[used++];
				kind.gameObject.SetActive(true);
				kind.Bind(entry);

				if (kind.SelectionItem) _selectionItems.Add(kind.SelectionItem);
			}

			for (var i = used; i < _kinds.Count; i++) _kinds[i].gameObject.SetActive(false);

			if (_selection) _selection.SetItems(_selectionItems);
		}

		// What the player cannot do right now is greyed, using the stage's own test — the one the server
		// refuses with. A greyed kind is also skipped by the pad's navigation, which uGUI does for free.
		private void RefreshInteractable()
		{
			foreach (var kind in _kinds)
			{
				if (kind.gameObject.activeSelf) kind.IsInteractable = _stage && _stage.IsWagerable((int)kind.ItemType);
			}
		}

		private void HandleSelectionChanged(UISelectionItem item) => RefreshCost(item);

		private void RefreshCost(UISelectionItem item)
		{
			var kind = KindOf(item);
			var database = _gameMode ? _gameMode.ItemDatabase : null;

			if (_chooseButton) _chooseButton.IsInteractable = kind && kind.IsInteractable;

			if (!kind || !_costLabel || !_player || !database
				|| !database.TryGetEntry(kind.ItemType, out var entry) || !entry.Effect)
			{
				SetCostVisible(false);
				return;
			}

			_costLabel.text = entry.Effect.PreviewHallucinationGain(_gameMode, _player, kind.ItemType).ToString();
			SetCostVisible(true);
		}

		private void HandleChoose()
		{
			if (_selection) HandleSubmitted(_selection.Selected);
		}

		private void HandleSubmitted(UISelectionItem item)
		{
			var kind = KindOf(item);
			if (!kind || !_gameMode || !_stage || !_stage.IsWagerable((int)kind.ItemType)) return;

			_gameMode.SubmitActionRPC(PokerActionType.Wager, (int)kind.ItemType);
		}

		private UIPokerBetKindButton KindOf(UISelectionItem item)
		{
			if (!item) return null;

			foreach (var kind in _kinds)
			{
				if (kind.SelectionItem == item) return kind;
			}

			return null;
		}

		private void HandleEscape()
		{
			var back = _back;
			Close();
			back?.Invoke();
		}

		private void SetCostVisible(bool visible)
		{
			if (_costGroup) _costGroup.alpha = visible ? 1f : 0f;
		}
	}
}
