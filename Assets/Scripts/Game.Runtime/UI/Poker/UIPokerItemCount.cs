using Game.Runtime.GameMode.Poker.Items;
using TMPro;
using UnityEngine;

namespace Game.Runtime.UI.Poker
{
	// How many item cards this player is holding, written into a label (the Items button's own). Which ones
	// is shown in the item picker. At a table that deals no items the optional content is switched off.
	public class UIPokerItemCount : UIPokerView
	{
		[Tooltip("Optional. Shown only at a table that deals items. A child, so this view keeps listening while it is hidden.")]
		[SerializeField] private GameObject _content;

		[SerializeField] private TMP_Text _countLabel;

		[Tooltip("What the label reads. {0} is how many items are held.")]
		[SerializeField] private string _format = "{0}";

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
			if (shown && _countLabel) _countLabel.text = string.Format(_format, inventory.Count.Value);
		}
	}
}
