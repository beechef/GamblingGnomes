using Game.Runtime.GameMode.Poker.Items;
using TMPro;
using UnityEngine;

namespace Game.Runtime.UI.Poker
{
	// How many item cards this player is holding, on the vitals corner. Which ones is shown only on their
	// turn, in the item picker. Switched off at a table that deals no items.
	public class UIPokerItemCount : UIPokerView
	{
		[Tooltip("Shown only at a table that deals items. A child, so this view keeps listening while it is hidden.")]
		[SerializeField] private GameObject _content;

		[SerializeField] private TMP_Text _countLabel;

		private void Awake()
		{
			if (_content) _content.SetActive(false);
		}

		protected override void OnBind()
		{
			if (LocalPlayer.ItemInventory) LocalPlayer.ItemInventory.OnCountChanged += Refresh;

			Refresh();
		}

		protected override void OnUnbind()
		{
			if (LocalPlayer.ItemInventory) LocalPlayer.ItemInventory.OnCountChanged -= Refresh;

			if (_content) _content.SetActive(false);
		}

		private void Refresh()
		{
			var inventory = LocalPlayer.ItemInventory;
			var shown = inventory && GameMode.FindModule<PokerItemModule>();

			if (_content) _content.SetActive(shown);
			if (shown && _countLabel) _countLabel.text = inventory.Count.Value.ToString();
		}
	}
}
