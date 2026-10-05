using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Localization
{
	/// <summary>
	/// One category of a language's strings — its menus, its items, its dialogue — as an asset of its own.
	/// A <see cref="LocaleTable"/> lists its sections and answers from all of them.
	/// </summary>
	/// <remarks>
	/// One asset per language held every string in a single list of hundreds, so finding the line to
	/// fix meant scrolling past everything else, and two people editing different areas touched the
	/// same file. A section is what a writer opens: one category of one language, and nothing else.
	/// Which sections exist is the project's choice; this plugin only reads them.
	/// </remarks>
	[CreateAssetMenu(menuName = "Localization/Locale Section", fileName = "Locale_")]
	public class LocaleSection : ScriptableObject
	{
		[TableList(AlwaysExpanded = true, ShowIndexLabels = false)]
		[SerializeField] private List<LocaleTable.Entry> _entries = new();

		public IReadOnlyList<LocaleTable.Entry> Entries => _entries;

#if UNITY_EDITOR
		/// <summary>Adds the key, or overwrites its value when it is already here.</summary>
		public void SetEntry(string key, string value)
		{
			for (var i = 0; i < _entries.Count; i++)
			{
				if (_entries[i].Key != key) continue;

				_entries[i] = new LocaleTable.Entry { Key = key, Value = value };
				return;
			}

			_entries.Add(new LocaleTable.Entry { Key = key, Value = value });
		}

		public void ClearEntries() => _entries.Clear();
#endif
	}
}
