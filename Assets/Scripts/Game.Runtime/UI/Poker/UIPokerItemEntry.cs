using DG.Tweening;
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

		[Header("Blocked")]
		[Tooltip("Tint of the icon frame, the tooltip frame and the reason while the item cannot be played.")]
		[SerializeField] private Color _blockedColor = new(0.7882353f, 0.24313726f, 0.2509804f, 1f);

		[SerializeField] private Image _frame;
		[SerializeField] private Image _tooltipFrame;

		[Tooltip("Shake of the tooltip when a blocked item is chosen.")]
		[SerializeField] private float _shakeDuration = 0.3f;

		[SerializeField] private float _shakeStrength = 12f;
		[SerializeField] private int _shakeVibrato = 20;

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

		private Color _frameColor;
		private Color _tooltipFrameColor;
		private Tween _shake;

		public PokerItem Item { get; private set; }

		public UISelectionItem SelectionItem => _selectionItem;

		private void Awake()
		{
			if (!_selectionItem) _selectionItem = GetComponent<UISelectionItem>();
			if (_frame) _frameColor = _frame.color;
			if (_tooltipFrame) _tooltipFrameColor = _tooltipFrame.color;
			if (_reasonLabel) _reasonLabel.color = _blockedColor;
			SetTooltipShown(false);
		}

		private void OnDestroy() => _shake.Kill(true);

		public void Bind(PokerItem item, PokerItemAvailability availability)
		{
			Item = item;
			name = $"Item_{item.Type}";

			// An item with no art of its own keeps the generic item icon authored on the prefab.
			if (_icon && item.Icon) _icon.sprite = item.Icon;

			// An item that cannot be played now is still pointed at and read, framed in red with the reason in
			// its tooltip; only choosing it is refused.
			if (_frame) _frame.color = availability.IsUsable ? _frameColor : _blockedColor;
			if (_tooltipFrame) _tooltipFrame.color = availability.IsUsable ? _tooltipFrameColor : _blockedColor;

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

		public void ShakeTooltip()
		{
			if (!_tooltip) return;

			_shake.Kill(true);
			_shake = _tooltip.transform.DOShakePosition(_shakeDuration, new Vector3(_shakeStrength, 0f, 0f), _shakeVibrato, 0f).SetUpdate(true);
		}
	}
}
