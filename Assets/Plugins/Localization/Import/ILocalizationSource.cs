using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace Localization.Import
{
	/// <summary>
	/// Somewhere the strings are written — a Google Sheet, a folder of CSV files, an Excel workbook —
	/// read as tabs of rows. A new format is a class implementing this; the importer that turns tabs
	/// into locale sections never learns which one it read.
	/// </summary>
	/// <remarks>
	/// Every tab is laid out the same way whatever holds it: the first row names the columns —
	/// <c>key</c>, then one per language headed by its locale code — and each row after it is one
	/// string. A source only reads; it never decides what a column means.
	/// </remarks>
	public interface ILocalizationSource
	{
		/// <summary>Reads every tab, or throws saying why it could not.</summary>
		Awaitable<IReadOnlyList<LocalizationTab>> ReadAsync(CancellationToken ct = default);
	}

	/// <summary>One tab of strings: its name, which names the section it fills, and its rows.</summary>
	public readonly struct LocalizationTab
	{
		public readonly string Name;

		/// <summary>Every row, the header first, as the cells were written.</summary>
		public readonly IReadOnlyList<string[]> Rows;

		public LocalizationTab(string name, IReadOnlyList<string[]> rows)
		{
			Name = name;
			Rows = rows;
		}
	}

	/// <summary>A source that could not be read, with a message fit to show whoever pressed Import.</summary>
	public class LocalizationSourceException : System.Exception
	{
		public LocalizationSourceException(string message) : base(message) { }
	}
}
