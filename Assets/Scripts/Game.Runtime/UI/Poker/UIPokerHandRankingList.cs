using System.Collections.Generic;
using Game.Runtime.GameMode.Poker.Hands;
using Sirenix.OdinInspector;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Game.Runtime.UI.Poker
{
	// Every hand the table recognises, best first, numbered from one. The list is the hand database itself,
	// so a hand added to or removed from the game appears or disappears here without touching the UI.
	//
	// The rows can be built in the editor with the button below, so the board is looked at while it is laid
	// out rather than only in Play mode. Whether that authored set is rebuilt again when the board opens is a
	// choice: on, a hand edited in the database shows up without anybody pressing the button again; off, the
	// board shows exactly what was built and saved.
	public class UIPokerHandRankingList : MonoBehaviour
	{
		[Header("Data")]
		[SerializeField] private PokerHandDatabase _database;

		[Header("References")]
		[SerializeField] private UIPokerHandRow _rowPrefab;

		[Tooltip("Layout group the rows are placed in.")]
		[SerializeField] private RectTransform _container;

		[Header("Build")]
		[Tooltip("On, the rows are re-bound from the database every time the board opens. Off, the rows built in the editor are shown as they were saved.")]
		[SerializeField] private bool _rebuildAtRuntime = true;

		private readonly List<UIPokerHandRow> _rows = new();

		// Rows already under the container — built in the editor — are adopted, so a runtime rebuild re-binds
		// them instead of adding a second set beside them.
		private void Awake()
		{
			if (_container) _container.GetComponentsInChildren(true, _rows);
		}

		private void OnEnable()
		{
			if (_rebuildAtRuntime) Rebuild();
		}

		// Rows already made are re-bound rather than destroyed, so reopening never draws a frame of both.
		private void Rebuild()
		{
			if (!_database || !_rowPrefab || !_container) return;

			var used = 0;

			foreach (var hand in _database.HandTypes)
			{
				if (!hand) continue;

				if (used == _rows.Count) _rows.Add(CreateRow());

				var row = _rows[used++];
				row.gameObject.SetActive(true);
				row.Bind(used, hand);
			}

			for (var i = used; i < _rows.Count; i++) _rows[i].gameObject.SetActive(false);
		}

		private UIPokerHandRow CreateRow()
		{
#if UNITY_EDITOR
			// Built in the editor, a row stays linked to its prefab, so restyling the row reaches the preview.
			if (!Application.isPlaying) return (UIPokerHandRow)PrefabUtility.InstantiatePrefab(_rowPrefab, _container);
#endif
			return Instantiate(_rowPrefab, _container);
		}

#if UNITY_EDITOR
		[Button("Build Preview", ButtonSizes.Large)]
		[PropertyOrder(-1)]
		private void BuildPreview()
		{
			if (!_container) return;

			Undo.RegisterFullObjectHierarchyUndo(_container.gameObject, "Build Hand Ranking Preview");

			// Always a fresh set: the preview is what the database says now, not what an earlier build left.
			for (var i = _container.childCount - 1; i >= 0; i--) Undo.DestroyObjectImmediate(_container.GetChild(i).gameObject);

			_rows.Clear();
			Rebuild();

			foreach (var row in _rows) Undo.RegisterCreatedObjectUndo(row.gameObject, "Build Hand Ranking Preview");
			EditorUtility.SetDirty(_container);
		}

		[Button("Clear Preview")]
		[PropertyOrder(-1)]
		private void ClearPreview()
		{
			if (!_container) return;

			for (var i = _container.childCount - 1; i >= 0; i--) Undo.DestroyObjectImmediate(_container.GetChild(i).gameObject);

			_rows.Clear();
			EditorUtility.SetDirty(_container);
		}
#endif
	}
}
