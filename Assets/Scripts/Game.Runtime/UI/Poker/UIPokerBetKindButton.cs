using Game.Runtime.GameMode.Poker.Items;
using Game.Runtime.UI.Button;
using Game.Runtime.UI.Selection;
using UnityEngine;

namespace Game.Runtime.UI.Poker
{
	// One kind of mushroom in the bet picker. It only says which kind it stands for and shows its UI
	// version, a variant of the prefab the pot puts on the table, so the two cannot drift into different caps.
	// Being pointed at and chosen belong to its UISelectionItem, so a mouse, the arrows and a pad all arrive
	// through one group; how it spins, swells and lights is the button's visuals, authored on the template.
	public class UIPokerBetKindButton : MonoBehaviour
	{
		[SerializeField] private UIButton _button;
		[SerializeField] private UISelectionItem _selectionItem;
		[SerializeField] private UIModelView _model;

		public PokerItemType ItemType { get; private set; }

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

		public void Bind(PokerItemDatabase.Entry entry)
		{
			ItemType = entry.Type;
			name = $"Kind_{entry.DisplayName}";

			if (_model) _model.Show(entry.UIPrefab);
		}
	}
}
