using System.Collections.Generic;
using Game.Runtime.GameMode.Poker.Hands;
using UnityEngine;

namespace Game.Runtime.UI.Poker
{
	// Every hand the table recognises, best first, numbered from one. The list is the hand database itself,
	// so a hand added to or removed from the game appears or disappears here without touching the UI.
	public class UIPokerHandRankingList : MonoBehaviour
	{
		[Header("Data")]
		[SerializeField] private PokerHandDatabase _database;

		[Header("References")]
		[SerializeField] private UIPokerHandRow _rowPrefab;

		[Tooltip("Layout group the rows are placed in.")]
		[SerializeField] private RectTransform _container;

		private readonly List<UIPokerHandRow> _rows = new();

		private void OnEnable() => Rebuild();

		// Rows already made are re-bound rather than destroyed, so reopening never draws a frame of both.
		private void Rebuild()
		{
			if (!_database || !_rowPrefab || !_container) return;

			var hands = _database.HandTypes;
			var used = 0;

			foreach (var hand in hands)
			{
				if (!hand) continue;

				if (used == _rows.Count) _rows.Add(Instantiate(_rowPrefab, _container));

				var row = _rows[used++];
				row.gameObject.SetActive(true);
				row.Bind(used, hand);
			}

			for (var i = used; i < _rows.Count; i++) _rows[i].gameObject.SetActive(false);
		}
	}
}
