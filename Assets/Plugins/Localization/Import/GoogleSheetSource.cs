using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Networking;

namespace Localization.Import
{
	/// <summary>
	/// A Google Sheet, each listed tab downloaded as CSV. The sheet must be shared as "anyone with
	/// the link can view": nothing here signs in.
	/// </summary>
	[Serializable]
	public class GoogleSheetSource : ILocalizationSource
	{
		[Serializable]
		public class Tab
		{
			[Tooltip("The section this tab fills: Locale_<CODE>_<Name>.")]
			public string Name;

			[Tooltip("The tab's gid — the number after #gid= in the address bar with the tab open.")]
			public string Gid;
		}

		[Tooltip("The spreadsheet's address, as copied from the browser.")]
		[SerializeField] private string _spreadsheetUrl;

		[SerializeField] private List<Tab> _tabs = new();

		public async Awaitable<IReadOnlyList<LocalizationTab>> ReadAsync(CancellationToken ct = default)
		{
			var id = SpreadsheetId(_spreadsheetUrl);
			if (string.IsNullOrEmpty(id)) throw new LocalizationSourceException("No spreadsheet address.");

			var tabs = new List<LocalizationTab>();

			foreach (var tab in _tabs)
			{
				if (string.IsNullOrWhiteSpace(tab.Gid))
				{
					throw new LocalizationSourceException($"Tab '{tab.Name}' has no gid.");
				}

				var url = $"https://docs.google.com/spreadsheets/d/{id}/export?format=csv&gid={tab.Gid.Trim()}";
				using var request = UnityWebRequest.Get(url);
				await request.SendWebRequest();
				ct.ThrowIfCancellationRequested();

				if (request.result != UnityWebRequest.Result.Success)
				{
					throw new LocalizationSourceException($"Tab '{tab.Name}' failed to download: {request.error}");
				}

				var text = request.downloadHandler.text;

				// A sheet that is not shared answers with a sign-in PAGE and a success code, which would
				// otherwise be read as one very strange string table.
				if (text.TrimStart().StartsWith("<", StringComparison.Ordinal))
				{
					throw new LocalizationSourceException($"Tab '{tab.Name}' came back as a web page, not a " +
					                                      "CSV — share the sheet as \"anyone with the link can view\".");
				}

				tabs.Add(new LocalizationTab(tab.Name, CsvText.Parse(text)));
			}

			return tabs;
		}

		/// <summary>The id between <c>/d/</c> and the next slash, or the whole text if it is only the id.</summary>
		private static string SpreadsheetId(string url)
		{
			if (string.IsNullOrWhiteSpace(url)) return null;

			const string marker = "/d/";
			var start = url.IndexOf(marker, StringComparison.Ordinal);
			if (start < 0) return url.Trim();

			start += marker.Length;
			var end = url.IndexOf('/', start);
			return end < 0 ? url.Substring(start) : url.Substring(start, end - start);
		}
	}
}
