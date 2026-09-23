using Game.Runtime.GameMode.Poker.Items;
using Game.Runtime.UI.Button;
using Game.Runtime.UI.Selection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Runtime.UI.Poker
{
	// One held item card in the item picker. It only says which item it stands for and draws its name and
	// icon; being pointed at and chosen belong to its UISelectionItem, so mouse and pad arrive through one group.
	public class UIPokerItemEntry : MonoBehaviour
	{
		[SerializeField] private UIButton _button;
		[SerializeField] private UISelectionItem _selectionItem;
		[SerializeField] private TMP_Text _label;

		[Tooltip("Optional. Keeps its authored sprite when the item has no icon.")]
		[SerializeField] private Image _icon;

		public PokerItem Item { get; private set; }

		public UISelectionItem SelectionItem => _selectionItem;

		public bool IsInteractable
		{
			get => _button && _button.IsInteractable;
			set { if (_button) _button.IsInteractable = value; }
		}

		private void Awake()
		{
			if (!_button) _button = GetComponent<UIButton>();
			if (!_selectionItem) _selectionItem = GetComponent<UISelectionItem>();
		}

		public void Bind(PokerItem item)
		{
			Item = item;
			name = $"Item_{item.Type}";

			if (_label) _label.text = item.DisplayName;

			// An item with no art of its own keeps the generic item icon authored on the prefab.
			if (_icon && item.Icon) _icon.sprite = item.Icon;
		}
	}
}
