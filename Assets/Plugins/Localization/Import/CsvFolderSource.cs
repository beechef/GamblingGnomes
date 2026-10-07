using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using UnityEngine;

namespace Localization.Import
{
	/// <summary>
	/// A folder of CSV files, one per tab, each named for the section it fills (<c>UI.csv</c>,
	/// <c>Spells.csv</c>). What the importer's Export writes, so the two round-trip.
	/// </summary>
	[Serializable]
	public class CsvFolderSource : ILocalizationSource
	{
		[Tooltip("The folder, relative to the project (the folder holding Assets) or absolute.")]
		[SerializeField] private string _folder = "LocalizationExport";

		public CsvFolderSource() { }

		public CsvFolderSource(string folder) => _folder = folder;

		public Awaitable<IReadOnlyList<LocalizationTab>> ReadAsync(CancellationToken ct = default)
		{
			var folder = ProjectPath(_folder);
			if (!Directory.Exists(folder)) throw new LocalizationSourceException($"No folder at {folder}.");

			var tabs = new List<LocalizationTab>();
			var files = Directory.GetFiles(folder, "*.csv");
			Array.Sort(files, StringComparer.Ordinal);

			foreach (var file in files)
			{
				tabs.Add(new LocalizationTab(Path.GetFileNameWithoutExtension(file), CsvText.Parse(File.ReadAllText(file))));
			}

			return Completed(tabs);
		}

		/// <summary>A path as the project sees it: relative ones start beside Assets.</summary>
		public static string ProjectPath(string path) =>
			Path.IsPathRooted(path) ? path : Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", path);

		private static Awaitable<IReadOnlyList<LocalizationTab>> Completed(IReadOnlyList<LocalizationTab> tabs)
		{
			var source = new AwaitableCompletionSource<IReadOnlyList<LocalizationTab>>();
			source.SetResult(tabs);
			return source.Awaitable;
		}
	}
}
