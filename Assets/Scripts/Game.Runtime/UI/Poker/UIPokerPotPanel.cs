using TMPro;
using Unity.Collections;
using UnityEngine;

namespace Game.Runtime.UI.Poker
{
	// The one number the whole table is playing for, over the middle of the board. It shows from the
	// moment there is anything to win and says nothing at all when the pot is empty — a "Pot: 0" between
	// hands is noise where the wireframe wants clear felt.
	public class UIPokerPotPanel : UIPokerView
	{
		[Header("Panel")]
		[SerializeField] private GameObject _panel;

		[Header("Labels")]
		[SerializeField] private TextMeshProUGUI _potLabel;

		private void Awake()
		{
			if (_panel) _panel.SetActive(false);
		}

		protected override void OnBind()
		{
			Data.Pot.OnValueChanged += HandlePotChanged;
			Data.OverlayStageId.OnValueChanged += HandleOverlayChanged;

			Refresh();
		}

		protected override void OnUnbind()
		{
			Data.OverlayStageId.OnValueChanged -= HandleOverlayChanged;
			Data.Pot.OnValueChanged -= HandlePotChanged;

			if (_panel) _panel.SetActive(false);
		}

		private void HandlePotChanged(int previous, int current) => Refresh();
		private void HandleOverlayChanged(FixedString32Bytes previous, FixedString32Bytes current) => Refresh();

		private void Refresh()
		{
			var pot = Data.Pot.Value;

			// An overlay stakes its own pot in its own currency and brings its own readout, so this one
			// stands down rather than sitting beside it — two lines both saying "Pot" is two pots the
			// player has to tell apart by the icon alone.
			var visible = pot > 0 && Data.OverlayStageId.Value.IsEmpty;

			if (_panel && _panel.activeSelf != visible) _panel.SetActive(visible);
			if (!visible) return;

			if (_potLabel) _potLabel.text = $"Pot: {pot}";
		}
	}
}
