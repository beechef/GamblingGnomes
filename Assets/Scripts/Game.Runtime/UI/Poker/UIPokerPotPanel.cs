using System.Collections.Generic;
using System.Text;
using Game.Runtime.GameMode.Poker;
using Game.Runtime.GameMode.Poker.Items;
using TMPro;
using Unity.Netcode;
using UnityEngine;

namespace Game.Runtime.UI.Poker
{
	// How many caps are on the table, over the middle of the board. It shows from the moment there is
	// anything staked and says nothing at all when the table is clear — a "Pot: 0" between hands is noise
	// where the wireframe wants clear felt.
	public class UIPokerPotPanel : UIPokerView
	{
		[Header("Panel")]
		[SerializeField] private GameObject _panel;

		[Header("Labels")]
		[SerializeField] private TextMeshProUGUI _potLabel;

		[Header("Breakdown")]
		[Tooltip("Optional: an itemised line under the total — how many of each kind the pot holds. Empty draws nothing.")]
		[SerializeField] private TextMeshProUGUI _breakdownLabel;

		private readonly Dictionary<PokerItemType, int> _typeCounts = new();
		private readonly StringBuilder _breakdown = new();

		private void Awake()
		{
			if (_panel) _panel.SetActive(false);
		}

		protected override void OnBind()
		{
			Data.OnPotItemsChanged += HandlePotItemsChanged;

			Refresh();
		}

		protected override void OnUnbind()
		{
			Data.OnPotItemsChanged -= HandlePotItemsChanged;

			if (_panel) _panel.SetActive(false);
		}

		private void HandlePotItemsChanged(NetworkListEvent<PokerBetItem> changeEvent) => Refresh();

		private void Refresh()
		{
			var pot = Data.PotItems.Count;
			var visible = pot > 0;

			if (_panel && _panel.activeSelf != visible) _panel.SetActive(visible);
			if (!visible) return;

			if (_potLabel) _potLabel.text = $"Pot: {pot}";
			if (_breakdownLabel) _breakdownLabel.text = BuildBreakdown();
		}

		// One line, database order, plain chips left unsaid: "3× Đỏ  1× Xanh" is what is on the table.
		private string BuildBreakdown()
		{
			var database = GameMode.ItemDatabase;
			if (!database) return string.Empty;

			_typeCounts.Clear();

			foreach (var item in Data.PotItems)
			{
				if (item.ItemType == PokerItemDatabase.PlainChip) continue;

				_typeCounts.TryGetValue(item.ItemType, out var count);
				_typeCounts[item.ItemType] = count + 1;
			}

			if (_typeCounts.Count == 0) return string.Empty;

			_breakdown.Clear();

			// Walked in the database order rather than by counting up through the values: the order rows sit
			// in is the reading order somebody authored, and a kind is named on its own row now.
			foreach (var entry in database.Entries)
			{
				if (entry == null) continue;
				if (!_typeCounts.TryGetValue(entry.Type, out var count)) continue;

				if (_breakdown.Length > 0) _breakdown.Append("  ");

				_breakdown.Append(count).Append("× ")
					.Append($"<color=#{ColorUtility.ToHtmlStringRGB(entry.Color)}>{entry.DisplayName}</color>");
			}

			return _breakdown.ToString();
		}
	}
}
