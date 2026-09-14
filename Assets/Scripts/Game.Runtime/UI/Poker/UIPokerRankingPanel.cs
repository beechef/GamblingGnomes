using System.Collections.Generic;
using Game.Runtime.GameMode.Poker.Player;
using UnityEngine;

namespace Game.Runtime.UI.Poker
{
	// The board that goes up when the hand is settled. It mirrors the table's showdown list rather than
	// working the ranking out for itself — the server already decided who placed where, and two clients
	// disagreeing about that would be worse than a frame of lag.
	public class UIPokerRankingPanel : UIPokerView
	{
		[Header("Panel")]
		[SerializeField] private GameObject _panel;

		[Header("Rows")]
		[Tooltip("One per finishing place, spawned as the board is filled — a table of two and a table of eight both fit.")]
		[SerializeField] private UIPokerRankingRow _rowPrefab;

		[Tooltip("Laid out by its own layout group, so this only has to add and remove children.")]
		[SerializeField] private RectTransform _rowContainer;

		private readonly List<UIPokerRankingRow> _rows = new();

		private void Awake()
		{
			if (_panel) _panel.SetActive(false);
		}

		protected override void OnBind()
		{
			Data.OnShowdownChanged += Refresh;

			Refresh();
		}

		protected override void OnUnbind()
		{
			Data.OnShowdownChanged -= Refresh;

			if (_panel) _panel.SetActive(false);
		}

		private void Refresh()
		{
			var showdown = Data.Showdown;

			// The board is up for exactly as long as the showdown is: it goes away because the stage clock
			// ran out and cleared the list, not because anybody dismissed it. There is nothing here to answer,
			// so there is nothing to close and no countdown worth drawing — the table simply moves on.
			var visible = showdown.Count > 0;

			if (_panel && _panel.activeSelf != visible) _panel.SetActive(visible);
			if (!visible) return;

			RefreshRows(showdown.Count);
		}

		private void RefreshRows(int count)
		{
			if (!_rowPrefab || !_rowContainer) return;

			while (_rows.Count < count) _rows.Add(Instantiate(_rowPrefab, _rowContainer));

			for (var i = 0; i < _rows.Count; i++)
			{
				var used = i < count;
				if (_rows[i].gameObject.activeSelf != used) _rows[i].gameObject.SetActive(used);

				if (!used) continue;

				var entry = Data.Showdown[i];
				_rows[i].SetEntry(entry, PokerPlayer.Find(entry.ClientId));
			}
		}
	}
}
