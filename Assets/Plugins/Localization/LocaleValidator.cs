#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Localization
{
	/// <summary>
	/// Editor-only: finds the mistakes a hand-edited string table invites — a key one language has and
	/// another lacks, an empty string, a template whose <c>{0}</c> placeholders differ between
	/// languages, a key written twice — and says so in the console the moment a table is saved.
	/// </summary>
	/// <remarks>
	/// Each of these fails quietly at runtime: a missing translation shows the default language, a
	/// placeholder mismatch makes <c>Format</c> fall back to the bare template, a duplicate means
	/// whichever section is read first wins. Nobody reads every row after an edit, so the check runs
	/// by itself on every save rather than waiting to be remembered.
	/// </remarks>
	public static class LocaleValidator
	{
		private static readonly Regex Placeholder = new(@"\{(\d+)(?:[^}]*)\}", RegexOptions.Compiled);

		/// <summary>Every problem in the catalog's tables, one line each. Empty means clean.</summary>
		public static List<string> Validate(LocalizationCatalog catalog)
		{
			var issues = new List<string>();
			if (!catalog) return issues;

			if (!catalog.TryGetLocale(catalog.DefaultLocaleCode, out var reference))
			{
				issues.Add($"The default locale '{catalog.DefaultLocaleCode}' is not in the catalog.");
				return issues;
			}

			var tables = new Dictionary<LocaleTable, Dictionary<string, string>>();
			foreach (var table in catalog.Locales)
			{
				if (table) tables[table] = Index(table, issues);
			}

			var referenceRows = tables[reference];

			foreach (var (table, rows) in tables)
			{
				foreach (var (key, value) in rows)
				{
					if (string.IsNullOrWhiteSpace(value)) issues.Add($"[{table.LocaleCode}] '{key}' is empty.");
				}

				if (table == reference) continue;

				foreach (var (key, value) in referenceRows)
				{
					if (!rows.TryGetValue(key, out var translated))
					{
						issues.Add($"[{table.LocaleCode}] '{key}' is missing (the default language has it).");
						continue;
					}

					var expected = Placeholders(value);
					var actual = Placeholders(translated);
					if (!expected.SetEquals(actual))
					{
						issues.Add($"[{table.LocaleCode}] '{key}' uses placeholders {{{string.Join(",", actual)}}} " +
						           $"but the default language uses {{{string.Join(",", expected)}}}.");
					}
				}

				foreach (var key in rows.Keys)
				{
					if (!referenceRows.ContainsKey(key)) issues.Add($"[{table.LocaleCode}] '{key}' is not in the default language.");
				}
			}

			return issues;
		}

		/// <summary>One language's rows by key, noting any key written more than once.</summary>
		private static Dictionary<string, string> Index(LocaleTable table, List<string> issues)
		{
			var rows = new Dictionary<string, string>();
			foreach (var entry in table.Entries)
			{
				if (string.IsNullOrEmpty(entry.Key)) continue;

				if (!rows.TryAdd(entry.Key, entry.Value)) issues.Add($"[{table.LocaleCode}] '{entry.Key}' is written more than once.");
			}

			return rows;
		}

		private static HashSet<string> Placeholders(string text)
		{
			var found = new HashSet<string>();
			if (string.IsNullOrEmpty(text)) return found;

			foreach (Match match in Placeholder.Matches(text)) found.Add(match.Groups[1].Value);
			return found;
		}

		/// <summary>Checks every catalog in the project and reports to the console.</summary>
		[MenuItem("Tools/Localization/Validate Strings")]
		public static void ValidateAll()
		{
			foreach (var guid in AssetDatabase.FindAssets($"t:{nameof(LocalizationCatalog)}"))
			{
				var path = AssetDatabase.GUIDToAssetPath(guid);
				var catalog = AssetDatabase.LoadAssetAtPath<LocalizationCatalog>(path);
				var issues = Validate(catalog);

				if (issues.Count == 0) continue;

				Debug.LogWarning($"[{nameof(LocaleValidator)}] {issues.Count} problem(s) in {path}:\n  " +
				                 string.Join("\n  ", issues), catalog);
			}
		}

		/// <summary>Runs the check whenever a table, a section or a catalog is saved.</summary>
		private class Watcher : AssetPostprocessor
		{
			private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
			{
				foreach (var path in imported)
				{
					var type = AssetDatabase.GetMainAssetTypeAtPath(path);
					if (type != typeof(LocaleSection) && type != typeof(LocaleTable) && type != typeof(LocalizationCatalog)) continue;

					ValidateAll();
					return;
				}
			}
		}
	}
}
#endif
