using System.Collections.Generic;
using System.Text;

namespace Localization.Import
{
	/// <summary>Reads and writes CSV the way spreadsheets export it.</summary>
	public static class CsvText
	{
		/// <summary>
		/// Splits CSV text into rows of cells, honouring quoted cells — which may hold commas, doubled
		/// quotes and line breaks, as a description written across two lines does.
		/// </summary>
		public static List<string[]> Parse(string text)
		{
			var rows = new List<string[]>();
			var row = new List<string>();
			var cell = new StringBuilder();
			var quoted = false;

			for (var i = 0; i < text.Length; i++)
			{
				var c = text[i];

				if (quoted)
				{
					if (c == '"' && i + 1 < text.Length && text[i + 1] == '"')
					{
						cell.Append('"');
						i++;
					}
					else if (c == '"')
					{
						quoted = false;
					}
					else
					{
						cell.Append(c);
					}

					continue;
				}

				switch (c)
				{
					case '"':
						quoted = true;
						break;
					case ',':
						row.Add(cell.ToString());
						cell.Clear();
						break;
					case '\r':
						break;
					case '\n':
						row.Add(cell.ToString());
						cell.Clear();
						rows.Add(row.ToArray());
						row.Clear();
						break;
					default:
						// A byte-order mark at the very start is not part of the first header.
						if (c != '﻿') cell.Append(c);
						break;
				}
			}

			if (cell.Length > 0 || row.Count > 0)
			{
				row.Add(cell.ToString());
				rows.Add(row.ToArray());
			}

			return rows;
		}

		/// <summary>One cell, quoted when it holds a comma, a quote or a line break.</summary>
		public static string Escape(string value)
		{
			value ??= "";
			var quoted = value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0;
			return quoted ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;
		}
	}
}
