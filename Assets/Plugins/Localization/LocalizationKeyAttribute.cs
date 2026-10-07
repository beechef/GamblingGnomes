using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Localization
{
	/// <summary>
	/// Marks a string field as a localization key, so the inspector offers the keys that exist
	/// instead of a blank text box.
	/// </summary>
	/// <remarks>
	/// A hand-typed key is a silent failure: it renders as <c>[key]</c> at run time, and only
	/// where someone happens to look. A dropdown cannot be spelled wrong.
	/// <para>
	/// The list comes from the tables themselves through <see cref="LocalizationKeySource"/>, so
	/// it is whatever the game actually has rather than a second list to keep in step. A key that
	/// is no longer in any table still shows as the stored value, so a stale one is visible rather
	/// than silently swapped for something else.
	/// </para>
	/// </remarks>
	[AttributeUsage(AttributeTargets.Field)]
	public class LocalizationKeyAttribute : PropertyAttribute
	{
	}

	/// <summary>
	/// Where the key dropdown gets its list.
	/// </summary>
	/// <remarks>
	/// Read from the catalog's default table rather than from a generated file, because the tables
	/// ARE the source of truth once seeded — a translator adding a key should see it offered
	/// without anyone re-running a generator.
	/// <para>
	/// One source shared by every key field, rather than a copy per component: the list was
	/// private to <see cref="LocalizedText"/>, so the second field that wanted it would have
	/// duplicated the lookup.
	/// </para>
	/// </remarks>
	public static class LocalizationKeySource
	{
		private const int LabelValueLength = 60;

		/// <summary>Every key the default table defines, sorted, with no duplicates.</summary>
		public static IEnumerable<string> Keys()
		{
			var keys = new List<string>();
			foreach (var pair in KeysWithValues()) keys.Add(pair.Key);
			return keys;
		}

		/// <summary>
		/// Every key the default table defines with what it says there, sorted by key, first row
		/// winning — so a dropdown can show what a key reads as rather than only its name.
		/// </summary>
		/// <remarks>
		/// Built once and kept until a project asset changes: every key field asked for it on every
		/// inspector repaint, re-finding the catalog and re-reading thousands of rows each time.
		/// </remarks>
		public static IReadOnlyList<KeyValuePair<string, string>> KeysWithValues()
		{
#if UNITY_EDITOR
			EnsureCache();
			return _pairs;
#else
			return Array.Empty<KeyValuePair<string, string>>();
#endif
		}

		/// <summary>Whether the default table defines <paramref name="key"/>. Always false in a player build.</summary>
		public static bool Contains(string key) => TryGetDefaultValue(key, out _);

		/// <summary>
		/// What a key says in the default table, for previewing a label outside Play mode, where
		/// <see cref="Localizer"/> has no catalog yet. Always false in a player build.
		/// </summary>
		public static bool TryGetDefaultValue(string key, out string value)
		{
#if UNITY_EDITOR
			if (!string.IsNullOrEmpty(key))
			{
				EnsureCache();
				return _valueByKey.TryGetValue(key, out value);
			}
#endif

			value = null;
			return false;
		}

		/// <summary>The dropdown line for <paramref name="key"/>, built once per cache.</summary>
		public static string CachedDropdownLabel(string key)
		{
#if UNITY_EDITOR
			EnsureCache();
			if (_labelByKey.TryGetValue(key, out var label)) return label;
#endif
			return FlattenForMenu(key ?? string.Empty);
		}

		/// <summary>
		/// One dropdown line: the key, then its value on one line and cut short. A slash is swapped
		/// for a lookalike, because both Unity's popup and Odin's dropdown read a slash as a submenu
		/// and would scatter one string across a tree.
		/// </summary>
		public static string DropdownLabel(string key, string value)
		{
			if (string.IsNullOrEmpty(value)) return FlattenForMenu(key);

			// Markup is noise in a menu line, and its closing slashes would otherwise need flattening.
			var line = Regex.Replace(value, "<[^>]*>", string.Empty).Replace("\r", string.Empty).Replace('\n', ' ');
			if (line.Length > LabelValueLength) line = line.Substring(0, LabelValueLength) + "…";

			return FlattenForMenu($"{key}   —   {line}");
		}

		private static string FlattenForMenu(string text) => text.Replace('/', '∕');

#if UNITY_EDITOR
		private static List<KeyValuePair<string, string>> _pairs;
		private static readonly Dictionary<string, string> _valueByKey = new();
		private static readonly Dictionary<string, string> _labelByKey = new();

		// Any asset change may be a table, a section or the catalog; rebuilding on the next read is
		// cheap next to doing it on every repaint.
		[UnityEditor.InitializeOnLoadMethod]
		private static void HookInvalidation() => UnityEditor.EditorApplication.projectChanged += Invalidate;

		/// <summary>Drops the cached keys, so the next read sees the tables as they are now.</summary>
		public static void Invalidate() => _pairs = null;

		private static void EnsureCache()
		{
			if (_pairs != null) return;

			_valueByKey.Clear();
			_labelByKey.Clear();

			foreach (var table in DefaultTables())
			{
				foreach (var entry in table.Entries)
				{
					if (!string.IsNullOrEmpty(entry.Key)) _valueByKey.TryAdd(entry.Key, entry.Value ?? string.Empty);
				}
			}

			_pairs = new List<KeyValuePair<string, string>>(_valueByKey);
			_pairs.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
			foreach (var pair in _pairs) _labelByKey[pair.Key] = DropdownLabel(pair.Key, pair.Value);
		}

		private static IEnumerable<LocaleTable> DefaultTables()
		{
			foreach (var guid in UnityEditor.AssetDatabase.FindAssets($"t:{nameof(LocalizationCatalog)}"))
			{
				var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
				var catalog = UnityEditor.AssetDatabase.LoadAssetAtPath<LocalizationCatalog>(path);

				if (catalog && catalog.TryGetLocale(catalog.DefaultLocaleCode, out var table) && table)
				{
					yield return table;
				}
			}
		}
#endif
	}
}
