using Game.Runtime.GameMode.Poker.Items;
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
		[SerializeField] private UISelectionItem _selectionItem;
		[SerializeField] private TMP_Text _label;

		[Tooltip("Optional. Keeps its authored sprite when the item has no icon.")]
		[SerializeField] private Image _icon;

		[Tooltip("Faded while the item cannot be played. Separate from the button's own group, so the entry can still be pointed at and read.")]
		[SerializeField] private CanvasGroup _usableGroup;

		[Range(0f, 1f)]
		[SerializeField] private float _unusableAlpha = 0.45f;

		public PokerItem Item { get; private set; }

		public UISelectionItem SelectionItem => _selectionItem;

		// An item that cannot be played now is still pointed at and read, with the reason under the
		// description; only choosing it is refused.
		public void SetUsable(bool usable)
		{
			if (_usableGroup) _usableGroup.alpha = usable ? 1f : _unusableAlpha;
		}

		private void Awake()
		{
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
