using System.Collections.Generic;
using Game.Runtime.GameMode.Config;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.UI.Config
{
	// Renders whatever entries it is handed — one section header per source, one typed row per entry.
	// A new module declaring configs appears here without this class hearing about it. Destroy-and-
	// rebuild is fine at this cadence: a build only happens when the definition set itself changes,
	// never per value and never per frame.
	public class UIMatchConfigList : MonoBehaviour
	{
		[Header("Layout")]
		[Tooltip("Laid out by its own vertical layout group; rows land under it in entry order.")]
		[Required]
		[SerializeField] private RectTransform _content;

		[Header("Prefabs")]
		[Required]
		[SerializeField] private UIMatchConfigSectionHeader _sectionHeaderPrefab;

		[Required]
		[SerializeField] private UIMatchConfigIntRow _intRowPrefab;

		[Required]
		[SerializeField] private UIMatchConfigBoolRow _boolRowPrefab;

		[Required]
		[SerializeField] private UIMatchConfigFloatRow _floatRowPrefab;

		private readonly List<UIMatchConfigRow> _rows = new();
		private readonly List<GameObject> _spawned = new();
		private bool _editable;

		public void Build(IReadOnlyList<MatchConfigEntry> entries, IMatchConfigValueAccess access)
		{
			Clear();

			if (entries == null) return;

			string currentSource = null;

			foreach (var entry in entries)
			{
				if (entry == null) continue;

				if (entry.SourceId != currentSource)
				{
					currentSource = entry.SourceId;

					var header = Instantiate(_sectionHeaderPrefab, _content);
					header.SetLabel(entry.SectionLabel);
					_spawned.Add(header.gameObject);
				}

				var row = InstantiateRow(entry);
				if (!row) continue;

				_spawned.Add(row.gameObject);
				_rows.Add(row);

				row.Bind(entry, access);
				row.SetEditable(_editable);
			}
		}

		public void SetEditable(bool editable)
		{
			_editable = editable;

			foreach (var row in _rows)
			{
				if (row) row.SetEditable(editable);
			}
		}

		public void RefreshValues()
		{
			foreach (var row in _rows)
			{
				if (row) row.Refresh();
			}
		}

		public void Clear()
		{
			foreach (var spawned in _spawned)
			{
				if (spawned) Destroy(spawned);
			}

			_spawned.Clear();
			_rows.Clear();
		}

		private UIMatchConfigRow InstantiateRow(MatchConfigEntry entry) => entry switch
		{
			MatchConfigInt => _intRowPrefab ? Instantiate(_intRowPrefab, _content) : null,
			MatchConfigBool => _boolRowPrefab ? Instantiate(_boolRowPrefab, _content) : null,
			MatchConfigFloat => _floatRowPrefab ? Instantiate(_floatRowPrefab, _content) : null,
			_ => null,
		};
	}
}
