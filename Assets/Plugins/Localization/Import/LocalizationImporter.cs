using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
#endif

namespace Localization.Import
{
	/// <summary>
	/// Pulls the strings from wherever they are written into the locale tables: each tab the
	/// <see cref="ILocalizationSource"/> reads becomes one <see cref="LocaleSection"/> per language.
	/// </summary>
	/// <remarks>
	/// The source is where strings are edited; the section assets are what a build reads, rewritten
	/// by every import — so an edit made to a section by hand is lost on the next one. Which source is
	/// a choice on this asset (Google Sheet, a folder of CSV, an Excel workbook, or any class
	/// implementing the interface); the import never branches on it. A tab's columns are matched to
	/// the catalog's languages by locale code; any other column (a translator's note) is ignored, as
	/// is a row whose key is empty or starts with <c>#</c>.
	/// </remarks>
	[CreateAssetMenu(menuName = "Localization/Importer", fileName = "LocalizationImporter")]
	public class LocalizationImporter : ScriptableObject
	{
		private const string KeyColumn = "key";

		[Title("Source")]
		[Tooltip("Where the strings are written.")]
		[SerializeReference]
		[Required]
		private ILocalizationSource _source;

		[Title("Tables")]
		[Required]
		[Tooltip("The languages to fill; each is matched to the column headed by its locale code.")]
		[SerializeField] private LocalizationCatalog _catalog;

		// Both folders are read only by editor code, so a player build sees them unused.
#pragma warning disable CS0414
		[Tooltip("Where the section assets live: <folder>/<CODE>/Locale_<CODE>_<Tab>.asset.")]
		[SerializeField] private string _sectionsFolder = "Assets/Settings/Localization";

		[Tooltip("Where Export writes one CSV per section, relative to the project. Outside Assets, so " +
		         "Unity does not import them.")]
		[SerializeField] private string _exportFolder = "LocalizationExport";
#pragma warning restore CS0414

		public LocalizationCatalog Catalog => _catalog;

#if UNITY_EDITOR
		/// <summary>
		/// Raised after an import has written the tables, for whatever derives something from the
		/// keys (a constants file) to follow them.
		/// </summary>
		public static event Action<LocalizationImporter> Imported;

		// Started and left to run: ImportAsync catches and reports everything itself.
		[Button("Import", ButtonSizes.Large), PropertyOrder(-1)]
		private void ImportButton() => _ = ImportAsync();

		[Button("Export To CSV"), PropertyOrder(-1)]
		[Tooltip("Writes the tables as they are now to CSV, one file per section — to move strings into " +
		         "a new source, or to keep a copy before an import overwrites them.")]
		private void ExportButton() => ExportToCsv();

		/// <summary>
		/// Reads every tab and, only once the whole source has been read, rewrites the sections — so a
		/// source that fails part way leaves the tables as they were rather than half imported.
		/// </summary>
		/// <returns>Whether the tables were rewritten.</returns>
		public async Awaitable<bool> ImportAsync()
		{
			try
			{
				if (_source == null || !_catalog)
				{
					Debug.LogError($"[{nameof(LocalizationImporter)}] Needs a source and a catalog.", this);
					return false;
				}

				EditorUtility.DisplayProgressBar("Localization", "Reading the source…", 0f);
				var tabs = await _source.ReadAsync();
				RefuseSpreadsheetErrors(tabs);

				EditorUtility.DisplayProgressBar("Localization", "Writing sections…", 1f);
				var keys = Write(tabs);
				AssetDatabase.SaveAssets();

				Debug.Log($"[{nameof(LocalizationImporter)}] Imported {keys} keys from {tabs.Count} tabs.", this);
				Imported?.Invoke(this);
				return true;
			}
			catch (LocalizationSourceException exception)
			{
				Debug.LogError($"[{nameof(LocalizationImporter)}] {exception.Message}", this);
				return false;
			}
			catch (Exception exception)
			{
				Debug.LogException(exception, this);
				return false;
			}
			finally
			{
				EditorUtility.ClearProgressBar();
			}
		}

		/// <summary>Fills each language's section of each tab, returning how many keys were written.</summary>
		private int Write(IReadOnlyList<LocalizationTab> tabs)
		{
			var keys = 0;
			var ordered = new Dictionary<LocaleTable, List<LocaleSection>>();

			foreach (var tab in tabs)
			{
				if (tab.Rows == null || tab.Rows.Count == 0) continue;

				var header = tab.Rows[0];
				var keyColumn = ColumnOf(header, KeyColumn);
				if (keyColumn < 0)
				{
					Debug.LogError($"[{nameof(LocalizationImporter)}] Tab '{tab.Name}' has no '{KeyColumn}' " +
					               "column; skipped.", this);
					continue;
				}

				for (var l = 0; l < _catalog.Locales.Count; l++)
				{
					var table = _catalog.Locales[l];
					if (!table) continue;

					var column = ColumnOf(header, table.LocaleCode);
					if (column < 0)
					{
						Debug.LogWarning($"[{nameof(LocalizationImporter)}] Tab '{tab.Name}' has no " +
						                 $"'{table.LocaleCode}' column; its section was left as it was.", this);
						continue;
					}

					var section = LoadOrCreateSection(table.LocaleCode, tab.Name);
					section.ClearEntries();

					for (var r = 1; r < tab.Rows.Count; r++)
					{
						var row = tab.Rows[r];
						var key = Cell(row, keyColumn).Trim();
						if (key.Length == 0 || key.StartsWith("#", StringComparison.Ordinal)) continue;

						section.SetEntry(key, Cell(row, column));
						if (l == 0) keys++;
					}

					EditorUtility.SetDirty(section);

					if (!ordered.TryGetValue(table, out var list)) ordered[table] = list = new List<LocaleSection>();
					list.Add(section);
				}
			}

			// The tabs' order is the table's reading order; a section no tab names keeps its place after them.
			foreach (var (table, sections) in ordered)
			{
				foreach (var existing in table.Sections)
				{
					if (existing && !sections.Contains(existing)) sections.Add(existing);
				}

				table.SetSections(sections);
				table.Invalidate();
				EditorUtility.SetDirty(table);
			}

			return keys;
		}

		private string SectionPath(string localeCode, string tabName)
		{
			var code = localeCode.ToUpperInvariant();
			return $"{_sectionsFolder}/{code}/Locale_{code}_{tabName}.asset";
		}

		private LocaleSection LoadOrCreateSection(string localeCode, string tabName)
		{
			var path = SectionPath(localeCode, tabName);
			var section = AssetDatabase.LoadAssetAtPath<LocaleSection>(path);
			if (section) return section;

			var folder = Path.GetDirectoryName(path)?.Replace('\\', '/');
			if (folder != null && !AssetDatabase.IsValidFolder(folder))
			{
				AssetDatabase.CreateFolder(Path.GetDirectoryName(folder)?.Replace('\\', '/'), Path.GetFileName(folder));
			}

			section = CreateInstance<LocaleSection>();
			AssetDatabase.CreateAsset(section, path);
			return section;
		}

		/// <summary>
		/// Writes one CSV per section of the first language — every language's column side by side —
		/// in exactly the layout an import reads.
		/// </summary>
		public void ExportToCsv()
		{
			if (!_catalog || _catalog.Locales.Count == 0 || !_catalog.Locales[0]) return;

			var folder = CsvFolderSource.ProjectPath(_exportFolder);
			Directory.CreateDirectory(folder);

			var locales = new List<LocaleTable>();
			foreach (var table in _catalog.Locales) if (table) locales.Add(table);

			var prefix = $"Locale_{locales[0].LocaleCode.ToUpperInvariant()}_";
			var written = 0;

			foreach (var first in locales[0].Sections)
			{
				if (!first || !first.name.StartsWith(prefix, StringComparison.Ordinal)) continue;

				var tabName = first.name.Substring(prefix.Length);
				var builder = new StringBuilder(KeyColumn);
				foreach (var table in locales) builder.Append(',').Append(table.LocaleCode);
				builder.Append("\r\n");

				var byLocale = new List<Dictionary<string, string>>();
				foreach (var table in locales)
				{
					var values = new Dictionary<string, string>();
					var section = AssetDatabase.LoadAssetAtPath<LocaleSection>(SectionPath(table.LocaleCode, tabName));
					if (section) foreach (var entry in section.Entries) values[entry.Key] = entry.Value;
					byLocale.Add(values);
				}

				foreach (var entry in first.Entries)
				{
					builder.Append(CsvText.Escape(entry.Key));
					foreach (var values in byLocale)
					{
						builder.Append(',').Append(CsvText.Escape(values.TryGetValue(entry.Key, out var v) ? v : ""));
					}

					builder.Append("\r\n");
				}

				File.WriteAllText(Path.Combine(folder, tabName + ".csv"), builder.ToString(), new UTF8Encoding(true));
				written++;
			}

			Debug.Log($"[{nameof(LocalizationImporter)}] Exported {written} sections to {folder}.", this);
			EditorUtility.RevealInFinder(folder);
		}

		/// <summary>
		/// A sheet reads a cell starting with + or = as a formula and exports its error (#ERROR!) in
		/// place of the text; imported, that error would be the string on screen. The whole import is
		/// refused instead, naming each cell to fix (start it with an apostrophe).
		/// </summary>
		private static void RefuseSpreadsheetErrors(IReadOnlyList<LocalizationTab> tabs)
		{
			var broken = new List<string>();
			foreach (var tab in tabs)
			{
				if (tab.Rows == null) continue;

				for (var r = 1; r < tab.Rows.Count; r++)
				{
					var row = tab.Rows[r];
					for (var c = 1; c < row.Length; c++)
					{
						var cell = row[c].Trim();
						// A leading = is a formula typed as text: the apostrophe went in front of the = rather than instead of it.
						if (cell is "#ERROR!" or "#NAME?" or "#REF!" or "#VALUE!" or "#N/A" or "#DIV/0!" or "#NUM!" || cell.StartsWith("="))
							broken.Add($"{tab.Name} row {r + 1} ({Cell(row, 0)}): {cell}");
					}
				}
			}

			if (broken.Count > 0)
				throw new LocalizationSourceException("The source holds formula errors; nothing was imported. Start these cells with an apostrophe:\n  " +
				                                      string.Join("\n  ", broken));
		}

		private static int ColumnOf(string[] header, string name)
		{
			for (var i = 0; i < header.Length; i++)
			{
				if (string.Equals(header[i].Trim(), name, StringComparison.OrdinalIgnoreCase)) return i;
			}

			return -1;
		}

		private static string Cell(string[] row, int column) => column < row.Length ? row[column] : "";
#endif
	}
}
