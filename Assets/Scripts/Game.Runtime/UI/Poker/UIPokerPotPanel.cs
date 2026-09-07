using System.Collections.Generic;
using System.Text;
using Game.Runtime.GameMode.Poker;
using Game.Runtime.GameMode.Poker.Items;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
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

		[Header("Breakdown")]
		[Tooltip("Optional: an itemised line under the total — how many of each kind the pot holds, read off the pot ledger. Empty draws nothing and the panel is the plain money pot it always was.")]
		[SerializeField] private TextMeshProUGUI _breakdownLabel;

		[Tooltip("Names and colours the breakdown by ItemTypeIndex. Only read when the breakdown label is set.")]
		[SerializeField] private PokerItemDatabase _itemDatabase;

		private readonly Dictionary<byte, int> _typeCounts = new();
		private readonly StringBuilder _breakdown = new();

		private void Awake()
		{
			if (_panel) _panel.SetActive(false);
		}

		protected override void OnBind()
		{
			Data.Pot.OnValueChanged += HandlePotChanged;
			Data.OnPotItemsChanged += HandlePotItemsChanged;
			Data.OverlayStageId.OnValueChanged += HandleOverlayChanged;

			Refresh();
		}

		protected override void OnUnbind()
		{
			Data.OverlayStageId.OnValueChanged -= HandleOverlayChanged;
			Data.OnPotItemsChanged -= HandlePotItemsChanged;
			Data.Pot.OnValueChanged -= HandlePotChanged;

			if (_panel) _panel.SetActive(false);
		}

		private void HandlePotChanged(int previous, int current) => Refresh();
		private void HandlePotItemsChanged(NetworkListEvent<PokerBetItem> changeEvent) => Refresh();
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
			if (_breakdownLabel) _breakdownLabel.text = BuildBreakdown();
		}

		// One line, database order, plain chips left unsaid: "3× Đỏ  1× Xanh" is what is on the plate,
		// and a pot of nothing but chips reads as the money pot it is.
		private string BuildBreakdown()
		{
			if (!_itemDatabase) return string.Empty;

			_typeCounts.Clear();

			foreach (var item in Data.PotItems)
			{
				if (item.ItemTypeIndex == PokerItemDatabase.PlainChip) continue;

				_typeCounts.TryGetValue(item.ItemTypeIndex, out var count);
				_typeCounts[item.ItemTypeIndex] = count + 1;
			}

			if (_typeCounts.Count == 0) return string.Empty;

			_breakdown.Clear();

			for (var i = 1; i <= _itemDatabase.Entries.Count && i <= byte.MaxValue; i++)
			{
				var itemType = (byte)i;

				if (!_typeCounts.TryGetValue(itemType, out var count)) continue;
				if (!_itemDatabase.TryGetEntry(itemType, out var entry)) continue;

				if (_breakdown.Length > 0) _breakdown.Append("  ");

				_breakdown.Append(count).Append("× ")
					.Append($"<color=#{ColorUtility.ToHtmlStringRGB(entry.Color)}>{entry.DisplayName}</color>");
			}

			return _breakdown.ToString();
		}
	}
}
