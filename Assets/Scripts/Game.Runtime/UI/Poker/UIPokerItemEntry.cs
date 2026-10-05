using Game.Runtime.GameMode.Poker.Items;
using Game.Runtime.UI.Selection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Runtime.UI.Poker
{
	// One held item card in the item picker: its icon, and a tooltip beside it with the name, description, cost
	// and why it cannot be played, shown while it is the marked entry. Being pointed at and chosen belong to its
	// UISelectionItem, so mouse and pad arrive through one group.
	public class UIPokerItemEntry : MonoBehaviour
	{
		[SerializeField] private UISelectionItem _selectionItem;

		[Tooltip("Optional. Keeps its authored sprite when the item has no icon.")]
		[SerializeField] private Image _icon;

		[Tooltip("Faded while the item cannot be played. Separate from the button's own group, so the entry can still be pointed at and read.")]
		[SerializeField] private CanvasGroup _usableGroup;

		[Range(0f, 1f)]
		[SerializeField] private float _unusableAlpha = 0.45f;

		[Header("Tooltip")]
		[Tooltip("Faded in while this entry is marked. Outside the icon's content, so it neither tilts with the hover nor dims with an unplayable item.")]
		[SerializeField] private CanvasGroup _tooltip;

		[SerializeField] private TMP_Text _nameLabel;
		[SerializeField] private TMP_Text _descriptionLabel;

		[Tooltip("Why the item cannot be played right now. Empty when it can.")]
		[SerializeField] private TMP_Text _reasonLabel;

		[Tooltip("Optional. Shows the item's hallucination cost, faded out for an item that costs nothing.")]
		[SerializeField] private CanvasGroup _costGroup;

		[SerializeField] private TMP_Text _costLabel;

		public PokerItem Item { get; private set; }

		public UISelectionItem SelectionItem => _selectionItem;

		private void Awake()
		{
			if (!_selectionItem) _selectionItem = GetComponent<UISelectionItem>();
			SetTooltipShown(false);
		}

		public void Bind(PokerItem item, PokerItemAvailability availability)
		{
			Item = item;
			name = $"Item_{item.Type}";

			// An item with no art of its own keeps the generic item icon authored on the prefab.
			if (_icon && item.Icon) _icon.sprite = item.Icon;

			// An item that cannot be played now is still pointed at and read, with the reason in its tooltip;
			// only choosing it is refused.
			if (_usableGroup) _usableGroup.alpha = availability.IsUsable ? 1f : _unusableAlpha;

			if (_nameLabel) _nameLabel.text = item.DisplayName;
			if (_descriptionLabel) _descriptionLabel.text = item.Description;
			if (_reasonLabel) _reasonLabel.text = availability.IsUsable ? string.Empty : availability.BlockReason;

			var cost = item.HallucinationCost;
			if (_costGroup) _costGroup.alpha = cost > 0 ? 1f : 0f;
			if (_costLabel && cost > 0) _costLabel.text = cost.ToString();
		}

		public void SetTooltipShown(bool shown)
		{
			if (_tooltip) _tooltip.alpha = shown ? 1f : 0f;
		}
	}
}
