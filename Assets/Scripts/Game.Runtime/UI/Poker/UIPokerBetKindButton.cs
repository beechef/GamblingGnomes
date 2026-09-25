using Game.Runtime.GameMode.Poker.BetItems;
using Game.Runtime.GameMode.Poker.Visual;
using Game.Runtime.Props;
using Game.Runtime.UI.Button;
using Game.Runtime.UI.Selection;
using UnityEngine;

namespace Game.Runtime.UI.Poker
{
	// One kind of mushroom in the bet picker. It only says which kind it stands for and shows it — the very
	// prefab the pot puts on the table, not a copy of it — so a cap on the picker and a cap on the table are
	// one thing drawn twice. That includes what happens to them: the cap is announced on
	// PokerBetItemCapRegistry like any other, so a hallucination that makes the caps smile makes this one
	// smile too, while the player is choosing which to put up.
	//
	// Being pointed at and chosen belong to its UISelectionItem, so a mouse, the arrows and a pad all arrive
	// through one group; how it spins, swells and lights is the button's visuals, authored on the prefab.
	public class UIPokerBetKindButton : MonoBehaviour
	{
		[SerializeField] private UIButton _button;
		[SerializeField] private UISelectionItem _selectionItem;
		[SerializeField] private UIModelView _model;

		private GameObject _registeredCap;
		private PropVariantController _variants;

		public PokerBetItemType BetItemType { get; private set; }

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

			if (_model) _model.OnModelChanged += HandleModelChanged;
		}

		private void OnDestroy()
		{
			if (_model) _model.OnModelChanged -= HandleModelChanged;

			Register(null);
		}

		public void Bind(PokerBetItemDatabase.Entry entry)
		{
			BetItemType = entry.Type;
			name = $"Kind_{entry.DisplayName}";

			if (_model) _model.Show(entry.WorldPrefab);
		}

		private void HandleModelChanged(GameObject model) => Register(model);

		private void Register(GameObject cap)
		{
			if (_registeredCap == cap) return;

			if (_variants) _variants.OnVariantChanged -= HandleVariantChanged;
			PokerBetItemCapRegistry.Remove(_registeredCap);

			_registeredCap = cap;
			_variants = cap ? cap.GetComponentInChildren<PropVariantController>(true) : null;

			PokerBetItemCapRegistry.Add(_registeredCap);
			if (_variants) _variants.OnVariantChanged += HandleVariantChanged;
		}

		// A look can switch pieces on and off, and the box is measured off what is drawn — refitted so a
		// smiling cap stays centred on the spin rather than wobbling round it.
		private void HandleVariantChanged(PropVariant variant)
		{
			if (_model) _model.Refit();
		}
	}
}
