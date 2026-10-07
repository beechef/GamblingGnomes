using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Localization
{
	/// <summary>
	/// One language: its code, its name, and the <see cref="LocaleSection"/>s that hold its strings.
	/// Adding a language is a new table and its sections, and translating is editing them — never a
	/// code change.
	/// </summary>
	/// <remarks>
	/// Keys are the vocabulary the game speaks; values are what one language says for them. A key
	/// missing from every section is reported by <see cref="Localizer"/> as the key itself in
	/// brackets, so an untranslated string is visible on screen rather than silently blank.
	/// <para>
	/// The strings live in sections, one asset per category, rather than in one list here: a single
	/// list of hundreds of rows was hard to find anything in. Rows kept on the table itself still
	/// count and are read first, for a string that belongs to no section.
	/// </para>
	/// </remarks>
	[CreateAssetMenu(menuName = "Localization/Locale Table", fileName = "Locale_")]
	public class LocaleTable : ScriptableObject
	{
		[Serializable]
		public struct Entry
		{
			[TableColumnWidth(220, Resizable = true)]
			public string Key;

			[TextArea(1, 4)]
			public string Value;
		}

		[Title("Locale")]
		[Tooltip("Short code this language is chosen by, e.g. en or vi. Stored in the player's prefs.")]
		[Required]
		[SerializeField] private string _localeCode = "en";

		[Tooltip("How this language names itself, shown on a language switch.")]
		[SerializeField] private string _displayName = "English";

		[Title("Sections")]
		[Tooltip("This language's strings, one asset per category. A key in two sections is read from " +
		         "the first that has it.")]
		[SerializeField] private List<LocaleSection> _sections = new();

		[Title("Loose strings")]
		[Tooltip("Strings that belong to no section. Read before the sections.")]
		[TableList(ShowIndexLabels = false)]
		[SerializeField] private List<Entry> _entries = new();

		private readonly Dictionary<string, string> _byKey = new();
		private bool _isIndexed;

		public string LocaleCode => _localeCode;

		public string DisplayName => _displayName;

		public IReadOnlyList<LocaleSection> Sections => _sections;

		/// <summary>Every row of this language — the loose ones, then each section's in order.</summary>
		public IEnumerable<Entry> Entries
		{
			get
			{
				foreach (var entry in _entries) yield return entry;

				foreach (var section in _sections)
				{
					if (!section) continue;

					foreach (var entry in section.Entries) yield return entry;
				}
			}
		}

		public bool TryGet(string key, out string value)
		{
			EnsureIndexed();
			return _byKey.TryGetValue(key, out value);
		}

		/// <summary>
		/// Drops the index so the next lookup re-reads every section. A section edited in the
		/// inspector does not tell the table it belongs to, and domain reload is off, so whoever
		/// starts localization calls this rather than trusting an index from an earlier session.
		/// </summary>
		public void Invalidate() => _isIndexed = false;

		/// <summary>
		/// Indexes the rows once. Rebuilt on demand rather than in OnEnable, because an asset edited
		/// in the inspector is not re-enabled and would otherwise serve a stale map.
		/// </summary>
		private void EnsureIndexed()
		{
			if (_isIndexed) return;
			_isIndexed = true;

			_byKey.Clear();
			foreach (var entry in Entries)
			{
				if (string.IsNullOrEmpty(entry.Key)) continue;

				// First row wins, so a duplicated key is an authoring slip rather than a string that
				// changes with list order.
				_byKey.TryAdd(entry.Key, entry.Value ?? string.Empty);
			}
		}

#if UNITY_EDITOR
		private void OnValidate() => _isIndexed = false;

		/// <summary>Editor-only: the sections this language reads, for tooling that seeds it.</summary>
		public void SetSections(IEnumerable<LocaleSection> sections)
		{
			_sections.Clear();
			_sections.AddRange(sections);
			_isIndexed = false;
		}
#endif
	}
}
