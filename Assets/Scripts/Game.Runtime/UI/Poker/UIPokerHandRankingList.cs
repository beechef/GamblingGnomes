using System.Collections.Generic;
using Game.Runtime.GameMode.Poker;
using Game.Runtime.GameMode.Poker.Hands;
using Sirenix.OdinInspector;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Game.Runtime.UI.Poker
{
	// Every hand the table recognises, best first, numbered from one. The list is the running mode's hand
	// database, so a hand added to or removed from the game appears or disappears here without touching the UI.
	// A house hand (one only a house rule makes) is crowned above the others and numbered zero, so the
	// standard ranking reads the same at every table.
	//
	// The rows can be built in the editor with the button below, from the preview database, so the board is
	// looked at while it is laid out rather than only in Play mode. Whether that authored set is rebuilt again
	// when the board opens is a choice: on, a hand edited in the database shows up without anybody pressing the
	// button again; off, the board shows exactly what was built and saved.
	public class UIPokerHandRankingList : MonoBehaviour
	{
		[Header("Data")]
		[Tooltip("What the editor preview is built from. At runtime the board lists the running mode's own hands.")]
		[SerializeField] private PokerHandDatabase _database;

		[Header("References")]
		[SerializeField] private UIPokerHandRow _rowPrefab;

		[Tooltip("Layout group the rows are placed in.")]
		[SerializeField] private RectTransform _container;

		[Tooltip("The row a house hand is drawn with.")]
		[SerializeField] private UIPokerHandRow _houseRowPrefab;

		[Tooltip("Where house hands go, above the standard ones. Switched off when the table has none.")]
		[SerializeField] private RectTransform _houseContainer;

		[Header("Build")]
		[Tooltip("On, the rows are re-bound from the database every time the board opens. Off, the rows built in the editor are shown as they were saved.")]
		[SerializeField] private bool _rebuildAtRuntime = true;

		private const int HouseRank = 0;

		private readonly List<UIPokerHandRow> _rows = new();
		private readonly List<UIPokerHandRow> _houseRows = new();

		private PokerHandDatabase Database
		{
			get
			{
				var mode = PokerGameMode.Instance;
				return mode && mode.HandDatabase ? mode.HandDatabase : _database;
			}
		}

		// Rows already under the containers — built in the editor — are adopted, so a runtime rebuild re-binds
		// them instead of adding a second set beside them.
		private void Awake()
		{
			if (_container) _container.GetComponentsInChildren(true, _rows);
			if (_houseContainer) _houseContainer.GetComponentsInChildren(true, _houseRows);
		}

		private void Start() => PokerGameMode.OnInstanceChanged += HandleInstanceChanged;

		private void OnDestroy() => PokerGameMode.OnInstanceChanged -= HandleInstanceChanged;

		private void OnEnable()
		{
			if (_rebuildAtRuntime) Rebuild(Database);
		}

		private void HandleInstanceChanged(PokerGameMode mode)
		{
			if (_rebuildAtRuntime && isActiveAndEnabled) Rebuild(Database);
		}

		// Rows already made are re-bound rather than destroyed, so reopening never draws a frame of both.
		private void Rebuild(PokerHandDatabase database)
		{
			if (!database || !_rowPrefab || !_container) return;

			var used = 0;
			var houseUsed = 0;

			foreach (var hand in database.HandTypes)
			{
				if (!hand) continue;

				if (hand.IsHouseHand && _houseContainer && _houseRowPrefab)
				{
					BindRow(_houseRows, houseUsed++, _houseRowPrefab, _houseContainer, HouseRank, hand);
					continue;
				}

				BindRow(_rows, used, _rowPrefab, _container, used + 1, hand);
				used++;
			}

			HideFrom(_rows, used);
			HideFrom(_houseRows, houseUsed);

			if (_houseContainer) _houseContainer.gameObject.SetActive(houseUsed > 0);
		}

		private void BindRow(List<UIPokerHandRow> rows, int index, UIPokerHandRow prefab, RectTransform container, int rank, PokerHandType hand)
		{
			if (index == rows.Count) rows.Add(CreateRow(prefab, container));

			var row = rows[index];
			row.gameObject.SetActive(true);
			row.Bind(rank, hand);
		}

		private static void HideFrom(List<UIPokerHandRow> rows, int used)
		{
			for (var i = used; i < rows.Count; i++) rows[i].gameObject.SetActive(false);
		}

		private static UIPokerHandRow CreateRow(UIPokerHandRow prefab, RectTransform container)
		{
#if UNITY_EDITOR
			// Built in the editor, a row stays linked to its prefab, so restyling the row reaches the preview.
			if (!Application.isPlaying) return (UIPokerHandRow)PrefabUtility.InstantiatePrefab(prefab, container);
#endif
			return Instantiate(prefab, container);
		}

#if UNITY_EDITOR
		[Button("Build Preview", ButtonSizes.Large)]
		[PropertyOrder(-1)]
		private void BuildPreview()
		{
			ClearPreview();

			Rebuild(_database);

			foreach (var row in _rows) Undo.RegisterCreatedObjectUndo(row.gameObject, "Build Hand Ranking Preview");
			foreach (var row in _houseRows) Undo.RegisterCreatedObjectUndo(row.gameObject, "Build Hand Ranking Preview");
			SetContainersDirty();
		}

		[Button("Clear Preview")]
		[PropertyOrder(-1)]
		private void ClearPreview()
		{
			// Always a fresh set: the preview is what the database says now, not what an earlier build left.
			ClearContainer(_container, _rows);
			ClearContainer(_houseContainer, _houseRows);
			SetContainersDirty();
		}

		private static void ClearContainer(RectTransform container, List<UIPokerHandRow> rows)
		{
			rows.Clear();
			if (!container) return;

			Undo.RegisterFullObjectHierarchyUndo(container.gameObject, "Clear Hand Ranking Preview");
			for (var i = container.childCount - 1; i >= 0; i--) Undo.DestroyObjectImmediate(container.GetChild(i).gameObject);
		}

		private void SetContainersDirty()
		{
			if (_container) EditorUtility.SetDirty(_container);
			if (_houseContainer) EditorUtility.SetDirty(_houseContainer);
		}
#endif
	}
}
