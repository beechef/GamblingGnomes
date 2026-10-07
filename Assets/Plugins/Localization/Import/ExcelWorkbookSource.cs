using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Xml.Linq;
using UnityEngine;

namespace Localization.Import
{
	/// <summary>
	/// An Excel workbook (<c>.xlsx</c>), each worksheet a tab named for the section it fills.
	/// </summary>
	/// <remarks>
	/// Read straight from the file's own XML — an xlsx is a zip of it — so no spreadsheet library is
	/// needed. Only what a string table uses is understood: text, shared text and plain numbers.
	/// Formulas are read as the value Excel last saved for them.
	/// </remarks>
	[Serializable]
	public class ExcelWorkbookSource : ILocalizationSource
	{
		[Tooltip("The workbook, relative to the project (the folder holding Assets) or absolute.")]
		[SerializeField] private string _path = "Localization.xlsx";

		public ExcelWorkbookSource() { }

		public ExcelWorkbookSource(string path) => _path = path;

		private static readonly XNamespace Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
		private static readonly XNamespace Relationships = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
		private static readonly XNamespace PackageRelationships = "http://schemas.openxmlformats.org/package/2006/relationships";

		public Awaitable<IReadOnlyList<LocalizationTab>> ReadAsync(CancellationToken ct = default)
		{
			var path = CsvFolderSource.ProjectPath(_path);
			if (!File.Exists(path)) throw new LocalizationSourceException($"No workbook at {path}.");

			var tabs = new List<LocalizationTab>();

			// Shared, so a workbook open in Excel can still be read.
			using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
			using var zip = new ZipArchive(stream, ZipArchiveMode.Read);

			var shared = ReadSharedStrings(zip);
			var targets = ReadSheetTargets(zip);
			var workbook = Load(zip, "xl/workbook.xml");

			foreach (var sheet in workbook.Descendants(Main + "sheet"))
			{
				var name = (string)sheet.Attribute("name");
				var id = (string)sheet.Attribute(Relationships + "id");
				if (name == null || id == null || !targets.TryGetValue(id, out var target)) continue;

				tabs.Add(new LocalizationTab(name, ReadRows(Load(zip, target), shared)));
			}

			var completion = new AwaitableCompletionSource<IReadOnlyList<LocalizationTab>>();
			completion.SetResult(tabs);
			return completion.Awaitable;
		}

		private static XDocument Load(ZipArchive zip, string entryName)
		{
			var entry = zip.GetEntry(entryName);
			if (entry == null) throw new LocalizationSourceException($"The workbook has no {entryName}.");

			using var reader = entry.Open();
			return XDocument.Load(reader);
		}

		/// <summary>Every cell's text written once and referred to by index, as Excel stores text.</summary>
		private static List<string> ReadSharedStrings(ZipArchive zip)
		{
			var strings = new List<string>();
			if (zip.GetEntry("xl/sharedStrings.xml") == null) return strings;

			// A formatted string is split into runs; its text is every run's text in order.
			foreach (var item in Load(zip, "xl/sharedStrings.xml").Descendants(Main + "si"))
			{
				strings.Add(string.Concat(item.Descendants(Main + "t").Select(t => t.Value)));
			}

			return strings;
		}

		/// <summary>Relationship id → the worksheet file it names.</summary>
		private static Dictionary<string, string> ReadSheetTargets(ZipArchive zip)
		{
			var targets = new Dictionary<string, string>();

			foreach (var relationship in Load(zip, "xl/_rels/workbook.xml.rels").Descendants(PackageRelationships + "Relationship"))
			{
				var id = (string)relationship.Attribute("Id");
				var target = (string)relationship.Attribute("Target");
				if (id == null || target == null) continue;

				// Relative to xl/, unless written from the package root.
				targets[id] = target.StartsWith("/", StringComparison.Ordinal) ? target.TrimStart('/') : "xl/" + target;
			}

			return targets;
		}

		private static List<string[]> ReadRows(XDocument sheet, List<string> shared)
		{
			var rows = new List<string[]>();

			foreach (var row in sheet.Descendants(Main + "row"))
			{
				// A cell says which column it is in (B3), because empty cells are simply left out.
				var cells = new SortedDictionary<int, string>();
				foreach (var cell in row.Elements(Main + "c"))
				{
					cells[ColumnIndex((string)cell.Attribute("r"), cells.Count)] = CellText(cell, shared);
				}

				var width = cells.Count == 0 ? 0 : cells.Keys.Max() + 1;
				var values = new string[width];
				for (var i = 0; i < width; i++) values[i] = cells.TryGetValue(i, out var text) ? text : "";

				rows.Add(values);
			}

			return rows;
		}

		private static string CellText(XElement cell, List<string> shared)
		{
			var type = (string)cell.Attribute("t");
			var value = cell.Element(Main + "v")?.Value ?? "";

			return type switch
			{
				"s" when int.TryParse(value, out var index) && index >= 0 && index < shared.Count => shared[index],
				"inlineStr" => string.Concat(cell.Descendants(Main + "t").Select(t => t.Value)),
				_ => value
			};
		}

		/// <summary>The zero-based column of a reference like <c>AB12</c>; the next free one without one.</summary>
		private static int ColumnIndex(string reference, int fallback)
		{
			if (string.IsNullOrEmpty(reference)) return fallback;

			var column = 0;
			foreach (var c in reference)
			{
				if (c < 'A' || c > 'Z') break;
				column = column * 26 + (c - 'A' + 1);
			}

			return column > 0 ? column - 1 : fallback;
		}
	}
}
