using System;
using System.Collections.Generic;
using UnityEngine;

namespace Localization
{
	/// <summary>
	/// The running language. Anything showing text asks here by key; switching language raises one
	/// event and every listener redraws.
	/// </summary>
	/// <remarks>
	/// Static and reset on load because domain reload is disabled: an instance that survived a Play
	/// session would serve the previous session's catalog.
	/// <para>
	/// A key with no translation comes back as <c>[key]</c> rather than empty, so a missing string is
	/// something a tester reads on screen instead of a control that looks broken.
	/// </para>
	/// </remarks>
	public static class Localizer
	{
		private const string PrefsKey = "localization.locale";

		private static LocalizationCatalog _catalog;
		private static LocaleTable _current;
		private static LocaleTable _fallback;
		private static readonly HashSet<string> _reportedMissing = new();

		/// <summary>
		/// Every string <see cref="Get"/> has handed out in the running language, already processed, so
		/// asking for the same key again allocates nothing. Cleared whenever what it holds could change:
		/// a new language, a new processor, a new catalog.
		/// </summary>
		private static readonly Dictionary<string, string> _processed = new();

		private static Func<string, string> _textProcessor;

		/// <summary>Reads the language the player last chose, empty for none. Player preferences until the game hands over its own storage.</summary>
		public static Func<string> LoadLocale = LoadFromPrefs;

		/// <summary>Remembers the chosen language. Player preferences until the game hands over its own storage.</summary>
		public static Action<string> SaveLocale = SaveToPrefs;

		/// <summary>Raised after the language changes, once the new table is in place.</summary>
		public static event Action OnLocaleChanged;

		public static bool IsInitialized => _catalog;

		public static string CurrentLocaleCode => _current ? _current.LocaleCode : string.Empty;

		public static string CurrentDisplayName => _current ? _current.DisplayName : string.Empty;

		public static IReadOnlyList<LocaleTable> Locales => _catalog ? _catalog.Locales : Array.Empty<LocaleTable>();

		/// <summary>
		/// The language <see cref="CycleLocale"/> would move to, in its own name. Empty when there is
		/// nowhere else to go, so a single-language build has nothing to offer.
		/// </summary>
		/// <remarks>
		/// A one-button switch is read as naming its destination rather than its current state — the
		/// button that says "Tiếng Việt" is the one that gives you Vietnamese. Offered here rather
		/// than worked out at the button, so the name and <see cref="CycleLocale"/> can never disagree
		/// about which language is next.
		/// </remarks>
		public static string NextDisplayName
		{
			get
			{
				var next = NextLocale();
				return next ? next.DisplayName : string.Empty;
			}
		}

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void ResetStatics()
		{
			_catalog = null;
			_current = null;
			_fallback = null;
			_reportedMissing.Clear();
			_processed.Clear();
			_textProcessor = null;
			OnLocaleChanged = null;
			LoadLocale = LoadFromPrefs;
			SaveLocale = SaveToPrefs;
		}

		private static string LoadFromPrefs() => PlayerPrefs.GetString(PrefsKey, string.Empty);

		private static void SaveToPrefs(string code) => PlayerPrefs.SetString(PrefsKey, code);

		/// <summary>
		/// Loads the catalog and picks the language the player last chose, or the default. Called
		/// once by whatever owns the boot sequence.
		/// </summary>
		public static void Initialize(LocalizationCatalog catalog)
		{
			if (!catalog)
			{
				Debug.LogError($"[{nameof(Localizer)}] Initialized without a catalog.");
				return;
			}

			_catalog = catalog;
			_processed.Clear();

			// Re-read every table: domain reload is off, so an index built in an earlier Play session
			// would otherwise survive an edit made to a section since.
			foreach (var locale in catalog.Locales)
			{
				if (locale) locale.Invalidate();
			}

			_catalog.TryGetLocale(catalog.DefaultLocaleCode, out _fallback);

			var saved = LoadLocale();
			if (string.IsNullOrEmpty(saved) || !TrySetLocale(saved)) TrySetLocale(catalog.DefaultLocaleCode);
		}

		public static void Deinitialize()
		{
			_catalog = null;
			_current = null;
			_fallback = null;
			_reportedMissing.Clear();
			_processed.Clear();
		}

		/// <summary>Switches language, remembers the choice, and tells every listener.</summary>
		public static bool TrySetLocale(string code)
		{
			if (!_catalog || !_catalog.TryGetLocale(code, out var locale)) return false;

			_current = locale;
			_processed.Clear();
			SaveLocale(code);

			OnLocaleChanged?.Invoke();
			return true;
		}

		/// <summary>Moves to the next language in the catalog's order, wrapping round.</summary>
		public static void CycleLocale()
		{
			var next = NextLocale();
			if (next) TrySetLocale(next.LocaleCode);
		}

		/// <summary>
		/// The language after the current one in the catalog's order, wrapping round. Null when the
		/// catalog is empty.
		/// </summary>
		/// <remarks>
		/// The one place the order is walked, so what a switch NAMES and what it DOES are the same
		/// answer rather than two copies of the same loop.
		/// </remarks>
		private static LocaleTable NextLocale()
		{
			var locales = Locales;
			if (locales.Count == 0) return null;

			var index = 0;
			for (var i = 0; i < locales.Count; i++)
			{
				if (locales[i] == _current) index = i;
			}

			return locales[(index + 1) % locales.Count];
		}

		/// <summary>
		/// Whether any table can answer a key, so a caller with a generic fallback can choose it
		/// without a bracketed key ever reaching the screen.
		/// </summary>
		public static bool Has(string key)
		{
			if (string.IsNullOrEmpty(key)) return false;

			return (_current && _current.TryGet(key, out _)) || (_fallback && _fallback.TryGet(key, out _));
		}

		/// <summary>
		/// Sets what every string is passed through before it is handed out — a rich text expander,
		/// say. Localization does not know what it does; null hands strings out as written.
		/// </summary>
		/// <remarks>
		/// A hook rather than a dependency: text processing and translation are separate jobs, each
		/// usable without the other, and whoever boots the game decides whether they meet.
		/// </remarks>
		public static void SetTextProcessor(Func<string, string> processor)
		{
			_textProcessor = processor;
			_processed.Clear();

			// Nothing to redraw with no language loaded: every label would draw its bracketed key
			// and warn. That is the teardown order — the processor's owner is destroyed after
			// localization has shut down, and any label still up redrew into the empty table.
			if (IsInitialized) OnLocaleChanged?.Invoke();
		}

		/// <summary>The current language's string for a key, passed through the text processor.</summary>
		/// <remarks>Cached per language, so a label redrawn every refresh costs a lookup, not a string.</remarks>
		public static string Get(string key)
		{
			if (string.IsNullOrEmpty(key)) return string.Empty;
			if (_processed.TryGetValue(key, out var cached)) return cached;

			if (!TryGetRaw(key, out var raw))
			{
				ReportMissing(key);
				return $"[{key}]";
			}

			var processed = Process(raw);
			_processed[key] = processed;
			return processed;
		}

		/// <summary>
		/// The current language's string for a key with its placeholders filled, then passed through
		/// the text processor once — so an argument may itself carry a tag.
		/// </summary>
		public static string Format(string key, params object[] args)
		{
			if (args == null || args.Length == 0) return Get(key);

			if (!TryGetRaw(key, out var template))
			{
				ReportMissing(key);
				return $"[{key}]";
			}

			try
			{
				return Process(string.Format(template, args));
			}
			catch (FormatException)
			{
				// A template whose placeholders disagree with the arguments is an authoring slip;
				// showing the template beats throwing from inside a UI refresh.
				return Get(key);
			}
		}

		/// <summary>
		/// The string as written in the current language, or the default language standing in for
		/// an untranslated key — so a language added later shows the default rather than brackets for
		/// everything it has not covered yet.
		/// </summary>
		private static bool TryGetRaw(string key, out string value)
		{
			if (_current && _current.TryGet(key, out value)) return true;
			if (_fallback && _fallback != _current && _fallback.TryGet(key, out value)) return true;

			value = null;
			return false;
		}

		private static string Process(string text) => _textProcessor != null ? _textProcessor(text) : text;

		private static void ReportMissing(string key)
		{
			if (!_reportedMissing.Add(key)) return;

			Debug.LogWarning($"[{nameof(Localizer)}] No string for '{key}' in '{CurrentLocaleCode}'.");
		}
	}
}
